/**
 * Renderizador de markdown mínimo (títulos, listas, negrito, itálico, código inline).
 * O texto vem de um LLM, então tudo é escapado antes de qualquer substituição —
 * o HTML produzido aqui é confiável apenas porque nenhuma entrada crua sobrevive ao escape.
 *
 * Usado pelo painel de AI Insights do dashboard e pelo laudo fitossanitário.
 */
export function renderMarkdown(text: string): string {
  const escape = (s: string) =>
    s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

  const inline = (s: string) =>
    escape(s)
      .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
      .replace(/\*(.+?)\*/g, '<em>$1</em>')
      .replace(/`(.+?)`/g, '<code class="ai-code">$1</code>');

  const lines = text.split('\n');
  const out: string[] = [];
  let inList = false;

  for (const raw of lines) {
    const line = raw.trimEnd();

    if (!line) {
      if (inList) { out.push('</ul>'); inList = false; }
      out.push('<br>');
      continue;
    }

    if (line.startsWith('### ')) {
      if (inList) { out.push('</ul>'); inList = false; }
      out.push(`<h5 class="ai-h5">${inline(line.slice(4))}</h5>`);
      continue;
    }
    if (line.startsWith('## ')) {
      if (inList) { out.push('</ul>'); inList = false; }
      out.push(`<h4 class="ai-h4">${inline(line.slice(3))}</h4>`);
      continue;
    }
    if (line.startsWith('# ')) {
      if (inList) { out.push('</ul>'); inList = false; }
      out.push(`<h3 class="ai-h3">${inline(line.slice(2))}</h3>`);
      continue;
    }

    if (/^[-*] /.test(line)) {
      if (!inList) { out.push('<ul class="ai-ul">'); inList = true; }
      out.push(`<li>${inline(line.slice(2))}</li>`);
      continue;
    }

    if (inList) { out.push('</ul>'); inList = false; }
    out.push(`<p class="ai-p">${inline(line)}</p>`);
  }

  if (inList) out.push('</ul>');
  return out.join('');
}
