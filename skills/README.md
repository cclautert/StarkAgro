An Angular SPA that interacts with an API at `http://192.168.68.33/v1/` to display sensor readings and manage Pivots, Users, and Sensors.

## Features
- **Dashboard**: Displays the last 15 sensor readings in a line chart with points.
- **Pivot Form**: Add a Pivot with UserId (int) and Name (string).
- **User Form**: Add a User with Name (string), Email (string), Password (string), and Active (boolean).
- **Sensor Form**: Add a Sensor with PivotId (string), UserId (int), Quadrante (int), and Code (string).

## Prerequisites
- Node.js (v18 or higher)
- Angular CLI (v18 or higher)
- API running at `http://192.168.68.33/v1/`

## Setup
1. Install dependencies:
   ```bash
   npm install
   ```
2. (Optional) Configure proxy for CORS:
   Create `proxy.conf.json`:
   ```json
   {
     "/v1/*": {
       "target": "http://192.168.68.33",
       "secure": false,
       "changeOrigin": true
     }
   }
   ```
   Update `angular.json`:
   ```json
   "serve": {
     "options": {
       "proxyConfig": "proxy.conf.json"
     }
   }
   ```
3. Run the application:
   ```bash
   ng serve
   ```
4. Open `http://localhost:4200` in your browser.

## Notes
- Ensure the API is accessible and supports CORS if needed.
- Basic error handling is implemented; enhance for production use.
- Add authentication if required by the API.
