export const environment = {
  production: false,
  /** Google OAuth 2.0 Client ID (from Google Cloud Console). Required for "Login with Google". */
  googleClientId: '33242671638-jcbmn942hc030633s34idmcmmj4o1tv8.apps.googleusercontent.com'
  // A chave pública VAPID não vive mais aqui: o front a busca em GET /v1/push/vapid-public-key.
  // Ela é metade de um par que só existe no .env do servidor — cravar no bundle já deixou os
  // dois lados com chaves de pares diferentes.
};
