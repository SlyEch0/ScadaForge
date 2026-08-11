using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using ScadaForge.Models;

namespace ScadaForge.Services;

/// <summary>
/// Production-ready OPC UA client for Allen-Bradley ControlLogix (and AVEVA OPC UA endpoints).
/// Uses the official OPC Foundation .NET Standard stack.
/// 
/// Typical ControlLogix endpoint: opc.tcp://192.168.1.10:4840
/// (requires ControlLogix firmware with OPC UA server enabled or a gateway such as FactoryTalk Linx / Kepware).
/// </summary>
public sealed class OpcUaClientService : IDisposable
{
    private readonly LogService _log;
    private ApplicationConfiguration? _config;
    private Session? _session;
    private Subscription? _subscription;

    public bool IsConnected => _session is { Connected: true };
    public string? EndpointUrl { get; private set; }

    public event Action<string, object?, TagQuality>? TagValueChanged;

    public OpcUaClientService(LogService log)
    {
        _log = log;
    }

    /// <summary>
    /// Connect to an OPC UA server (ControlLogix, AVEVA, or any compliant server).
    /// </summary>
    public async Task ConnectAsync(string endpointUrl, CancellationToken ct = default)
    {
        if (IsConnected) await DisconnectAsync();

        EndpointUrl = endpointUrl;
        _log.Info($"Connecting to OPC UA: {endpointUrl}");

        try
        {
            _config = new ApplicationConfiguration
            {
                ApplicationName = "SCADA Forge",
                ApplicationUri = $"urn:ScadaForge:{Environment.MachineName}",
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier
                    {
                        StoreType = "Directory",
                        StorePath = "CertificateStores/MachineDefault",
                        SubjectName = "CN=SCADA Forge, O=SCADA Forge"
                    },
                    TrustedPeerCertificates = new CertificateTrustList
                    {
                        StoreType = "Directory",
                        StorePath = "CertificateStores/UAApplications"
                    },
                    RejectedCertificateStore = new CertificateTrustList
                    {
                        StoreType = "Directory",
                        StorePath = "CertificateStores/RejectedCertificates"
                    },
                    AutoAcceptUntrustedCertificates = true, // For lab / development only
                    AddAppCertToTrustedStore = true
                },
                TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 },
                CertificateValidator = new CertificateValidator()
            };

            await _config.Validate(ApplicationType.Client);

            // Accept untrusted certs in development (tighten for production)
            _config.CertificateValidator.CertificateValidation += (s, e) =>
            {
                e.Accept = true;
            };

            var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointUrl, useSecurity: false);
            var endpointConfiguration = EndpointConfiguration.Create(_config);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);

            _session = await Session.Create(
                _config,
                endpoint,
                updateBeforeConnect: false,
                sessionName: "ScadaForgeSession",
                sessionTimeout: 60000,
                identity: new UserIdentity(new AnonymousIdentityToken()),
                preferredLocales: null,
                ct);

            _log.Info($"Connected to OPC UA Server: {_session.Endpoint.EndpointUrl}");
        }
        catch (Exception ex)
        {
            _log.Error($"OPC UA connect failed: {ex.Message}");
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            if (_subscription != null)
            {
                await _session!.RemoveSubscriptionAsync(_subscription);
                _subscription.Dispose();
                _subscription = null;
            }

            if (_session != null)
            {
                await _session.CloseAsync();
                _session.Dispose();
                _session = null;
            }

            _log.Info("OPC UA disconnected");
        }
        catch (Exception ex)
        {
            _log.Warn($"OPC UA disconnect warning: {ex.Message}");
        }
    }

    /// <summary>
    /// Create a subscription for a set of NodeIds. Values are pushed via TagValueChanged.
    /// </summary>
    public async Task SubscribeAsync(IEnumerable<(string Name, string NodeId)> tags, int publishingIntervalMs = 500)
    {
        if (_session is null || !_session.Connected)
            throw new InvalidOperationException("Not connected to OPC UA server");

        _subscription = new Subscription(_session.DefaultSubscription)
        {
            PublishingInterval = publishingIntervalMs,
            DisplayName = "ScadaForgeSubscription"
        };

        foreach (var (name, nodeId) in tags)
        {
            var item = new MonitoredItem(_subscription.DefaultItem)
            {
                DisplayName = name,
                StartNodeId = new NodeId(nodeId),
                AttributeId = Attributes.Value,
                SamplingInterval = publishingIntervalMs
            };

            item.Notification += OnMonitoredItemNotification;
            _subscription.AddItem(item);
        }

        _session.AddSubscription(_subscription);
        await _subscription.CreateAsync();
        _log.Info($"Subscribed to {_subscription.MonitoredItemCount} OPC UA tags");
    }

    private void OnMonitoredItemNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
    {
        if (e.NotificationValue is not MonitoredItemNotification notification) return;

        var value = notification.Value.Value;
        var quality = StatusCode.IsGood(notification.Value.StatusCode)
            ? TagQuality.Good
            : StatusCode.IsUncertain(notification.Value.StatusCode)
                ? TagQuality.Uncertain
                : TagQuality.Bad;

        TagValueChanged?.Invoke(item.DisplayName, value, quality);
    }

    public async Task WriteAsync(string nodeId, object value)
    {
        if (_session is null || !_session.Connected)
            throw new InvalidOperationException("Not connected");

        var nodeToWrite = new WriteValue
        {
            NodeId = new NodeId(nodeId),
            AttributeId = Attributes.Value,
            Value = new DataValue(new Variant(value))
        };

        var response = await _session.WriteAsync(null, new WriteValueCollection { nodeToWrite }, CancellationToken.None);
        if (StatusCode.IsBad(response.Results[0]))
            throw new ServiceResultException(response.Results[0]);
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        _config = null;
    }
}
