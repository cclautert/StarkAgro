# CORS Fix for Local Development

## Issue
The frontend is getting "Failed to fetch" errors when trying to connect to the API. This is typically a CORS (Cross-Origin Resource Sharing) issue.

## Solution Applied
Updated the CORS configuration to explicitly allow localhost origins for development.

## Steps to Fix

### 1. Ensure the API is Running
The API must be running before the frontend can connect to it.

**Option A: Run from Visual Studio**
- Open the `AgripeWebAPI` project in Visual Studio
- Press F5 or click "Run"
- The API should start on `https://localhost:7162` or `http://localhost:5115`

**Option B: Run from Command Line**
```powershell
cd AgripeWebAPI
dotnet run
```

The API will start on:
- HTTPS: `https://localhost:7162`
- HTTP: `http://localhost:5115`

### 2. Verify API is Accessible
Open a browser and navigate to:
- `https://localhost:7162/swagger` (if Swagger is enabled)
- Or `http://localhost:5115/swagger`

**Note:** If you get a certificate warning for HTTPS, click "Advanced" and "Proceed to localhost" (this is safe for local development).

### 3. Frontend Configuration
The frontend services are configured to use:
- `https://localhost:7162/v1/` (HTTPS - default)
- Alternative: `http://localhost:5115/v1/` (HTTP - if HTTPS has certificate issues)

If you're having HTTPS certificate issues, you can temporarily change the frontend services to use HTTP:

**In `api.service.ts`, `user.service.ts`, `sensor.service.ts`, `pivot.service.ts`:**
```typescript
private baseUrl = 'http://localhost:5115/v1/'; // Use HTTP instead of HTTPS
```

### 4. CORS Configuration
The CORS policy has been updated to allow:
- `http://localhost:4200` (Angular dev server)
- `https://localhost:4200`
- `http://localhost:5115` (API HTTP)
- `https://localhost:7162` (API HTTPS)

### 5. Common Issues and Solutions

#### Issue: "Failed to fetch" or CORS error
**Solution:**
1. Make sure the API is running
2. Check that the API URL in frontend services matches the running API URL
3. Restart both the API and frontend after making changes

#### Issue: HTTPS certificate errors
**Solution:**
- Option 1: Trust the development certificate:
  ```powershell
  dotnet dev-certs https --trust
  ```
- Option 2: Use HTTP instead of HTTPS in frontend services (change `baseUrl` to `http://localhost:5115/v1/`)

#### Issue: "URL scheme must be 'http' or 'https'"
**Solution:**
- Check that the `baseUrl` in your services doesn't have typos
- Ensure it starts with `http://` or `https://`
- Make sure there are no extra spaces or characters

### 6. Testing the Connection
1. Start the API: `dotnet run` in `AgripeWebAPI` folder
2. Start the frontend: `ng serve` in `AgripeWebUI` folder
3. Open browser to `http://localhost:4200`
4. Try to login or make an API call
5. Check browser console (F12) for any errors

### 7. Verify CORS is Working
Open browser DevTools (F12) → Network tab:
- Look for the API request
- Check the Response Headers for `Access-Control-Allow-Origin`
- It should show your frontend origin (e.g., `http://localhost:4200`)

## Quick Fix Checklist
- [ ] API is running (`dotnet run` in AgripeWebAPI folder)
- [ ] API is accessible (test in browser: `https://localhost:7162/swagger`)
- [ ] Frontend `baseUrl` matches API URL
- [ ] Both API and frontend are restarted after changes
- [ ] Browser console shows no CORS errors
- [ ] Network tab shows `Access-Control-Allow-Origin` header

## Production Notes
For production, update the CORS policy in `ApiConfig.cs` to only allow your production frontend domain.
