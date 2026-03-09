# OAuth 2.0 Setup (Google)

The API supports OAuth 2.0 **Authorization Code** flow with **Google** as an external identity provider.

## Backend (API)

1. **Google Cloud Console**
   - Go to [Google Cloud Console](https://console.cloud.google.com/) → APIs & Services → Credentials.
   - Create an **OAuth 2.0 Client ID** (Application type: **Web application**).
   - Add **Authorized redirect URIs** (must match exactly what the frontend uses), e.g.:
     - `https://localhost:4200/login/callback`
     - `http://localhost:4200/login/callback`
     - `http://localhost/login/callback` (when UI is served on port 80, e.g. Docker)
   - Copy the **Client ID** and **Client secret**.

2. **Configuration**
   - Add to `appsettings.Development.json` or User Secrets:
   ```json
   "OAuth": {
     "Google": {
       "ClientId": "YOUR_GOOGLE_CLIENT_ID",
       "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET",
       "AllowedRedirectUris": "https://localhost:4200/login/callback,http://localhost:4200/login/callback,http://localhost/login/callback"
     }
   }
   ```
   - `AllowedRedirectUris` must include every redirect URI you use (comma-separated). These should match the URIs configured in Google Cloud Console.

## Frontend (Angular)

1. In `src/environments/environment.ts` set your Google **Client ID** (public, safe in frontend):
   ```ts
   export const environment = {
     production: false,
     googleClientId: 'YOUR_GOOGLE_CLIENT_ID'
   };
   ```
2. For production, set `googleClientId` in `src/environments/environment.prod.ts` and add your production callback URL to the API `AllowedRedirectUris` and to Google Cloud Console.

## Flow

1. User clicks **Entrar com Google** on the login page.
2. Browser redirects to Google consent screen.
3. After approval, Google redirects to `/login/callback?code=...`.
4. The Angular callback component sends `provider`, `code`, and `redirectUri` to `POST /v1/Auth/external-login`.
5. The API exchanges the code with Google, gets user info, creates or finds the user, and returns a JWT.
6. The frontend stores the JWT and redirects to `/home`.
