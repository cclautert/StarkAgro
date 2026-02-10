# DBHub: Fix "Could not connect" to SQL Server

The error means nothing is accepting TCP connections on `localhost:1433`. Do the following on the machine where SQL Server is installed.

---

## 1. Check if port 1433 is in use

In PowerShell:

```powershell
Get-NetTCPConnection -LocalPort 1433 -ErrorAction SilentlyContinue | Select-Object LocalAddress, LocalPort, State
```

- **No output** → Nothing is listening on 1433. Enable TCP/IP and set the port (steps 2–3).
- **State = Listen** → Something is listening. If DBHub still fails, try `host = "127.0.0.1"` in `dbhub.toml` or check firewall.

---

## 2. Enable TCP/IP for SQL Server

1. Press **Win + R**, type **SQLServerManager17.msc** (or **SQLServerManager16.msc** for SQL 2022/2019), press Enter.  
   - Or open **SQL Server Configuration Manager** from the Start menu.
2. Expand **SQL Server Network Configuration** → click **Protocols for &lt;YourInstance&gt;** (e.g. *MSSQLSERVER* or *SQLEXPRESS*).
3. Right‑click **TCP/IP** → **Enable**.
4. In the left pane, go to **SQL Server Services**, right‑click **SQL Server (&lt;YourInstance&gt;)** → **Restart**.

---

## 3. Set TCP port to 1433 (default instance)

1. Under **SQL Server Network Configuration** → **Protocols for &lt;YourInstance&gt;**.
2. Right‑click **TCP/IP** → **Properties** → tab **IP Addresses**.
3. Scroll to **IPAll**:
   - Set **TCP Dynamic Ports** to *blank* (empty).
   - Set **TCP Port** to **1433**.
4. OK, then **restart** the SQL Server service again (as in step 2.4).

If you use a **named instance** (e.g. *SQLEXPRESS*), it may use a **dynamic port**. In that case:
- Note the port shown in **TCP Dynamic Ports** (e.g. 49152), or set **TCP Port** to **1433** and clear **TCP Dynamic Ports**.
- In `dbhub.toml` use that port and, if required, `instanceName = "SQLEXPRESS"` (see DBHub docs).

---

## 4. Try 127.0.0.1 in dbhub.toml

If `localhost` still fails, in `dbhub.toml` set:

```toml
host = "127.0.0.1"
```

---

## 5. Firewall (if connecting from another PC later)

For now, if SQL Server is on the same machine, firewall is less likely the cause. If you later use `host = "ZEUS"`, on the SQL Server machine allow inbound TCP **1433** in Windows Firewall.

---

## 6. Confirm your API can connect

Your AgripeWeb API uses a connection string (e.g. `Server=ZEUS;...`). If the API runs and connects, SQL Server is running. The remaining issue is usually **TCP/IP disabled** or **wrong port** for DBHub. After enabling TCP and setting port 1433 (step 2–3), run again:

```bat
.\run-dbhub.bat
```
