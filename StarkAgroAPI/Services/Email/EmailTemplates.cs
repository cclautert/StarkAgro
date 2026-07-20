using System.Net;

namespace StarkAgroAPI.Services.Email
{
    /// <summary>
    /// Casca HTML dos e-mails. Estilos inline porque cliente de e-mail ignora folha de estilo.
    /// </summary>
    public static class EmailTemplates
    {
        public static string Escape(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);

        public static string Wrap(string title, string content) => $"""
            <!DOCTYPE html>
            <html lang="pt-BR">
            <body style="margin:0;padding:24px;background:#F4F6F4;font-family:Segoe UI,Arial,sans-serif;color:#1C1E1B">
              <div style="max-width:560px;margin:0 auto;background:#fff;border:1px solid #DCE3DC;border-radius:10px;overflow:hidden">
                <div style="background:#1B5E20;padding:16px 20px">
                  <div style="color:#fff;font-size:12px;letter-spacing:2px;opacity:.85">STARKAGRO</div>
                  <div style="color:#fff;font-size:19px;font-weight:700;margin-top:2px">{Escape(title)}</div>
                </div>
                <div style="padding:20px;font-size:14px;line-height:1.6">
                  {content}
                </div>
                <div style="padding:12px 20px;border-top:1px solid #DCE3DC;color:#5F6B60;font-size:11px">
                  Você recebeu este e-mail porque tem uma conta no StarkAgro.
                </div>
              </div>
            </body>
            </html>
            """;
    }
}
