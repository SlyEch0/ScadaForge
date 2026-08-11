# SCADA Forge – Developer Toolkit

**Modern HMI / SCADA design & runtime toolkit** for **AVEVA System Platform 2023** + **Allen-Bradley ControlLogix**.

This is the Option B baseline: a fully working WPF application you can open in Visual Studio, run immediately in Simulation mode, and connect to a real ControlLogix PLC via OPC UA.

---

## Quick Start (Simulation)

1. Clone the repo  
   `git clone https://github.com/SlyEch0/ScadaForge.git`
2. Open `ScadaForge.sln` in Visual Studio 2022 (17.8+)
3. Restore NuGet packages
4. Press **F5**

The Water Treatment Plant overview launches with live simulation values and P-101 already selected.

---

## Connecting to a Real Allen-Bradley ControlLogix PLC

SCADA Forge talks to ControlLogix (and CompactLogix) exclusively over **OPC UA**.  
There are two practical paths in 2026:

### Path A – Native OPC UA Server (Firmware v36 / v37+)  
**Best when available** – no extra software required.

**Supported controllers (approximate):**
- ControlLogix 5580: 1756-L82E and higher (L81E does **not** support OPC UA nodes)
- CompactLogix 5380 series (5069-L310ER and higher)
- Some GuardLogix variants

**Steps:**

1. **Upgrade firmware** to v36 or preferably **v37+** (v37 adds a simple checkbox in Studio 5000).
2. In **Studio 5000 Logix Designer**:
   - Go to **Controller Properties → OPC UA** tab (v37+)
   - Check **Enable OPC UA**
   - Select the Ethernet port
   - (Optional) Configure security policy – start with **None** for lab testing
3. Download the project to the controller.
4. The OPC UA endpoint will be:
   ```
   opc.tcp://<controller-ip-address>:4840
   ```
5. In SCADA Forge:
   - Paste the endpoint into the top-right text box
   - Click **Connect OPC UA**

**Node addressing example** (common pattern):
```
ns=6;s=Program:MainProgram.MyTag
ns=6;s=ControllerTagName
```

> Note: Native node/tag limits are relatively low on smaller controllers. For large tag counts prefer Path B.

### Path B – Gateway (Recommended for most production systems)

Use a proven OPC UA gateway that talks EtherNet/IP to the ControlLogix and exposes a full OPC UA server.

**Popular choices:**

| Gateway                        | Notes                                      | Typical Endpoint              |
|--------------------------------|--------------------------------------------|-------------------------------|
| **Kepware KEPServerEX**        | Excellent AB driver suite, very common     | `opc.tcp://PC-IP:49320`       |
| **FactoryTalk Linx Gateway**   | Official Rockwell solution                 | `opc.tcp://PC-IP:4840`        |
| Softing, Matrikon, etc.        | Also work well                             | Vendor specific               |

**Typical Kepware setup:**
1. Install KEPServerEX + Allen-Bradley ControlLogix Ethernet driver
2. Create a channel → device (IP of the ControlLogix, path `1,0` for slot 0)
3. Import tags from Studio 5000 or browse online
4. Enable the OPC UA server in Kepware (default port often 49320)
5. Point SCADA Forge at `opc.tcp://<kepware-pc-ip>:49320`

**FactoryTalk Linx Gateway:**
- Requires FactoryTalk Services Platform + Linx Gateway license
- Exposes Logix tags over OPC UA (and OPC DA)
- Good choice when you already live in the FactoryTalk ecosystem

### Security Notes (Important)

- For initial testing, most people start with **Security Policy = None** / Anonymous.
- Production systems should use **Sign & Encrypt** (Basic256Sha256) + certificates.
- SCADA Forge currently auto-accepts untrusted certificates for development convenience. Tighten this before production deployment.
- Make sure Windows Firewall (or the plant firewall) allows TCP 4840 (or the gateway port) from the SCADA Forge machine to the PLC/gateway.

### Troubleshooting Checklist

| Symptom                        | Likely Cause / Fix                                      |
|--------------------------------|---------------------------------------------------------|
| Connection timeout             | Firewall, wrong IP, OPC UA not enabled on controller    |
| Bad certificate                | Accept once or install trusted cert                     |
| No tags visible                | Wrong NodeId format or tags not exposed                 |
| Quality = Bad / Uncertain      | Controller in Program mode or communication loss        |
| “Not connected” after connect  | Check the OUTPUT log panel for detailed OPC UA errors   |

---

## AVEVA System Platform 2023 Integration Path

The `Tag` model and `TagService` are deliberately designed so your existing **aaMXAccessLib** / MXAccess / LMX Proxy code can be added as another data source without touching the UI or graphic objects.

Typical next step:
1. Add the MXAccess assemblies (watch the 32-bit preference)
2. Implement an `AvevaMxAccessTagService` that maps Galaxy attribute paths into the same VTQ model
3. Switch the data source the same way we switch between Simulation and OPC UA today

---

## Project Structure

```
ScadaForge/
├── Models/           Tag (VTQ), Motor, Tank, Valve, Instrument, Project
├── Services/         SimulationEngine, OpcUaClientService, TagService, LogService
├── ViewModels/       MainViewModel
├── Views/            MainWindow (process canvas + properties panel)
└── App.xaml          Dark industrial theme
```

---

## Requirements

- Windows 10 / 11
- .NET 8 SDK
- Visual Studio 2022 with “.NET desktop development” workload
- (Optional) Real ControlLogix or CompactLogix with OPC UA capability **or** a gateway (Kepware / FT Linx Gateway)
- (Optional) AVEVA System Platform 2023 + MXAccess Toolkit for Galaxy integration

---

Built for real industrial use.  
Questions or next features → open an issue on the repo.
