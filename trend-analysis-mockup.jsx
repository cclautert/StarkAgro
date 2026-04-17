import { useState, useEffect, useRef } from "react";
import {
  LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip,
  ReferenceLine, ReferenceArea, ResponsiveContainer, Legend, Area, ComposedChart
} from "recharts";

// ─── Utilitários de Regressão Linear ──────────────────────────────────────────
function linearRegression(data) {
  const n = data.length;
  if (n < 2) return { slope: 0, intercept: data[0]?.value ?? 50 };
  const sumX = data.reduce((s, _, i) => s + i, 0);
  const sumY = data.reduce((s, d) => s + d.value, 0);
  const sumXY = data.reduce((s, d, i) => s + i * d.value, 0);
  const sumXX = data.reduce((s, _, i) => s + i * i, 0);
  const slope = (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);
  const intercept = (sumY - slope * sumX) / n;
  return { slope, intercept };
}

function movingAverage(data, window = 3) {
  return data.map((d, i) => {
    const slice = data.slice(Math.max(0, i - window + 1), i + 1);
    const avg = slice.reduce((s, x) => s + x.value, 0) / slice.length;
    return { ...d, movingAvg: +avg.toFixed(1) };
  });
}

// ─── Geração de dados simulados ────────────────────────────────────────────────
function generateData(days, trendType) {
  const now = new Date();
  const data = [];
  let base = 62;

  for (let i = days - 1; i >= 0; i--) {
    const d = new Date(now);
    d.setDate(d.getDate() - i);

    if (trendType === "queda") base -= 1.2 + Math.random() * 0.5;
    else if (trendType === "subida") base += 0.8 + Math.random() * 0.5;
    else base += (Math.random() - 0.5) * 2;

    base = Math.max(15, Math.min(95, base));
    const noise = (Math.random() - 0.5) * 6;

    data.push({
      date: `${d.getDate().toString().padStart(2, "0")}/${(d.getMonth() + 1)
        .toString()
        .padStart(2, "0")}`,
      value: +Math.max(10, Math.min(98, base + noise)).toFixed(1),
    });
  }
  return data;
}

// ─── Tooltip customizado ───────────────────────────────────────────────────────
const CustomTooltip = ({ active, payload, label }) => {
  if (!active || !payload?.length) return null;
  return (
    <div style={{
      background: "#1e293b", border: "1px solid #334155",
      borderRadius: 8, padding: "10px 14px", fontSize: 12, color: "#e2e8f0"
    }}>
      <p style={{ fontWeight: 600, marginBottom: 6, color: "#94a3b8" }}>{label}</p>
      {payload.map((p, i) => p.value != null && (
        <p key={i} style={{ color: p.color, margin: "2px 0" }}>
          {p.name}: <strong>{typeof p.value === "number" ? p.value.toFixed(1) : p.value}%</strong>
        </p>
      ))}
    </div>
  );
};

// ─── Badge de tendência ────────────────────────────────────────────────────────
function TrendBadge({ slope }) {
  if (Math.abs(slope) < 0.3) return (
    <span style={{ background: "#1e40af22", color: "#60a5fa", border: "1px solid #3b82f6", borderRadius: 20, padding: "3px 12px", fontSize: 12, fontWeight: 600 }}>
      ➡ Estável
    </span>
  );
  if (slope > 0) return (
    <span style={{ background: "#16653422", color: "#4ade80", border: "1px solid #22c55e", borderRadius: 20, padding: "3px 12px", fontSize: 12, fontWeight: 600 }}>
      ↑ Subindo {Math.abs(slope).toFixed(1)}%/dia
    </span>
  );
  return (
    <span style={{ background: "#7f1d1d22", color: "#f87171", border: "1px solid #ef4444", borderRadius: 20, padding: "3px 12px", fontSize: 12, fontWeight: 600 }}>
      ↓ Caindo {Math.abs(slope).toFixed(1)}%/dia
    </span>
  );
}

// ─── Card de métrica ───────────────────────────────────────────────────────────
function MetricCard({ label, value, unit, sub, color }) {
  return (
    <div style={{
      background: "#1e293b", border: "1px solid #334155", borderRadius: 10,
      padding: "14px 18px", flex: "1 1 140px", minWidth: 130
    }}>
      <div style={{ fontSize: 11, color: "#64748b", marginBottom: 4 }}>{label}</div>
      <div style={{ fontSize: 24, fontWeight: 700, color: color ?? "#e2e8f0" }}>
        {value}<span style={{ fontSize: 13, fontWeight: 400, marginLeft: 2 }}>{unit}</span>
      </div>
      {sub && <div style={{ fontSize: 11, color: "#94a3b8", marginTop: 2 }}>{sub}</div>}
    </div>
  );
}

// ─── Componente Principal ──────────────────────────────────────────────────────
export default function App() {
  const LIMITE_SUPERIOR = 80;
  const LIMITE_INFERIOR = 40;

  const [days, setDays] = useState(14);
  const [trendType, setTrendType] = useState("queda");
  const [showTrend, setShowTrend] = useState(true);
  const [showMA, setShowMA] = useState(true);
  const [showProjection, setShowProjection] = useState(true);
  const [rawData, setRawData] = useState([]);

  useEffect(() => {
    setRawData(generateData(days, trendType));
  }, [days, trendType]);

  const withMA = movingAverage(rawData, 3);
  const { slope, intercept } = linearRegression(rawData);

  // Dados com linha de tendência
  const withTrend = withMA.map((d, i) => ({
    ...d,
    trend: +(intercept + slope * i).toFixed(1),
  }));

  // Projeção: 5 pontos futuros
  const projectionDays = 5;
  const lastDate = rawData[rawData.length - 1]?.date ?? "";
  const projectionData = [];
  for (let p = 1; p <= projectionDays; p++) {
    const projValue = intercept + slope * (rawData.length - 1 + p);
    const clamped = Math.max(0, Math.min(100, projValue));
    const margin = Math.min(p * 2.5, 15);
    projectionData.push({
      date: `+${p}d`,
      projMin: +(clamped - margin).toFixed(1),
      projMax: +(clamped + margin).toFixed(1),
      projMid: +clamped.toFixed(1),
    });
  }

  // Combina histórico + projeção para o gráfico unificado
  const fullData = [
    ...withTrend.map(d => ({ ...d, projMin: null, projMax: null, projMid: null })),
    ...projectionData.map(d => ({ ...d, value: null, movingAvg: null, trend: null })),
  ];

  // Estatísticas
  const values = rawData.map(d => d.value);
  const avg = values.length ? (values.reduce((a, b) => a + b, 0) / values.length).toFixed(1) : "-";
  const minVal = values.length ? Math.min(...values).toFixed(1) : "-";
  const maxVal = values.length ? Math.max(...values).toFixed(1) : "-";
  const lastVal = values[values.length - 1]?.toFixed(1) ?? "-";
  const proj5 = projectionData[4]?.projMid?.toFixed(1) ?? "-";

  const alertCount = values.filter(v => v < LIMITE_INFERIOR || v > LIMITE_SUPERIOR).length;

  return (
    <div style={{ background: "#0f172a", minHeight: "100vh", padding: "20px 24px", fontFamily: "'Inter', sans-serif", color: "#e2e8f0" }}>

      {/* ── Header ── */}
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 20, flexWrap: "wrap", gap: 12 }}>
        <div>
          <div style={{ fontSize: 11, color: "#64748b", letterSpacing: 1, textTransform: "uppercase", marginBottom: 2 }}>AgripeWeb · Pivô Central — Quadrante 1</div>
          <h1 style={{ fontSize: 20, fontWeight: 700, margin: 0, color: "#f1f5f9" }}>Dashboard · Sensor S-01 — Umidade</h1>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <button style={{ background: "#1e293b", border: "1px solid #334155", color: "#94a3b8", borderRadius: 6, padding: "6px 14px", fontSize: 12, cursor: "pointer" }}>← Voltar</button>
          <button style={{ background: "#1e293b", border: "1px solid #334155", color: "#94a3b8", borderRadius: 6, padding: "6px 14px", fontSize: 12, cursor: "pointer" }}>⚙ Configuração</button>
        </div>
      </div>

      {/* ── Controles de simulação ── */}
      <div style={{ background: "#1e293b", border: "1px solid #334155", borderRadius: 10, padding: "12px 16px", marginBottom: 16, display: "flex", flexWrap: "wrap", gap: 16, alignItems: "center" }}>
        <span style={{ fontSize: 12, color: "#64748b", fontWeight: 600 }}>🎛 Simular cenário:</span>
        {["queda", "subida", "estável"].map(t => (
          <button key={t} onClick={() => setTrendType(t)} style={{
            background: trendType === t ? "#0ea5e9" : "#0f172a", color: trendType === t ? "#fff" : "#94a3b8",
            border: `1px solid ${trendType === t ? "#0ea5e9" : "#334155"}`, borderRadius: 6,
            padding: "5px 14px", fontSize: 12, cursor: "pointer", fontWeight: trendType === t ? 600 : 400,
            textTransform: "capitalize"
          }}>
            {t === "queda" ? "↓ Queda" : t === "subida" ? "↑ Subida" : "➡ Estável"}
          </button>
        ))}
        <span style={{ fontSize: 12, color: "#64748b", marginLeft: 8, fontWeight: 600 }}>Período:</span>
        {[7, 14, 30].map(d => (
          <button key={d} onClick={() => setDays(d)} style={{
            background: days === d ? "#6366f1" : "#0f172a", color: days === d ? "#fff" : "#94a3b8",
            border: `1px solid ${days === d ? "#6366f1" : "#334155"}`, borderRadius: 6,
            padding: "5px 12px", fontSize: 12, cursor: "pointer", fontWeight: days === d ? 600 : 400
          }}>
            {d} dias
          </button>
        ))}
      </div>

      {/* ── Cards de métricas ── */}
      <div style={{ display: "flex", gap: 12, marginBottom: 16, flexWrap: "wrap" }}>
        <MetricCard label="Última leitura" value={lastVal} unit="%" color="#38bdf8" />
        <MetricCard label="Média do período" value={avg} unit="%" color="#a78bfa" />
        <MetricCard label="Mínimo" value={minVal} unit="%" color="#fb923c" />
        <MetricCard label="Máximo" value={maxVal} unit="%" color="#34d399" />
        <MetricCard label="Projeção em 5 dias" value={proj5} unit="%" color="#facc15" sub="± margem de confiança" />
        <MetricCard
          label="Alertas no período"
          value={alertCount}
          unit=" leituras"
          color={alertCount > 0 ? "#f87171" : "#4ade80"}
          sub={alertCount > 0 ? "fora dos limites" : "dentro dos limites"}
        />
      </div>

      {/* ── Gráfico principal ── */}
      <div style={{ background: "#1e293b", border: "1px solid #334155", borderRadius: 12, padding: "16px 20px", marginBottom: 16 }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 14, flexWrap: "wrap", gap: 10 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
            <span style={{ fontWeight: 600, fontSize: 14 }}>Leituras do Sensor</span>
            <TrendBadge slope={slope} />
          </div>
          <div style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
            {[
              { key: "showTrend", label: "Tendência", state: showTrend, set: setShowTrend, color: "#facc15" },
              { key: "showMA", label: "Média Móvel", state: showMA, set: setShowMA, color: "#a78bfa" },
              { key: "showProjection", label: "Projeção", state: showProjection, set: setShowProjection, color: "#fb923c" },
            ].map(opt => (
              <label key={opt.key} style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 12, cursor: "pointer", color: opt.state ? opt.color : "#475569", userSelect: "none" }}>
                <input type="checkbox" checked={opt.state} onChange={e => opt.set(e.target.checked)}
                  style={{ accentColor: opt.color, width: 14, height: 14 }} />
                {opt.label}
              </label>
            ))}
          </div>
        </div>

        <ResponsiveContainer width="100%" height={340}>
          <ComposedChart data={fullData} margin={{ top: 10, right: 20, left: 0, bottom: 5 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#1e3a5f22" />
            <XAxis dataKey="date" tick={{ fill: "#64748b", fontSize: 11 }} tickLine={false} axisLine={{ stroke: "#334155" }} />
            <YAxis domain={[0, 100]} tick={{ fill: "#64748b", fontSize: 11 }} tickLine={false} axisLine={false}
              tickFormatter={v => `${v}%`} />
            <Tooltip content={<CustomTooltip />} />

            {/* Zona vermelha — abaixo do limite inferior */}
            <ReferenceArea y1={0} y2={LIMITE_INFERIOR} fill="#ef444412" />
            {/* Zona verde — entre limites */}
            <ReferenceArea y1={LIMITE_INFERIOR} y2={LIMITE_SUPERIOR} fill="#22c55e10" />
            {/* Zona azul — acima do limite superior */}
            <ReferenceArea y1={LIMITE_SUPERIOR} y2={100} fill="#3b82f612" />

            {/* Linhas de limite */}
            <ReferenceLine y={LIMITE_SUPERIOR} stroke="#3b82f6" strokeDasharray="5 5" strokeWidth={1.5}
              label={{ value: `Limite Superior (${LIMITE_SUPERIOR}%)`, fill: "#3b82f6", fontSize: 10, position: "insideTopRight" }} />
            <ReferenceLine y={LIMITE_INFERIOR} stroke="#ef4444" strokeDasharray="5 5" strokeWidth={1.5}
              label={{ value: `Limite Inferior (${LIMITE_INFERIOR}%)`, fill: "#ef4444", fontSize: 10, position: "insideBottomRight" }} />

            {/* Faixa de projeção */}
            {showProjection && (
              <Area dataKey="projMax" fill="#fb923c22" stroke="none" name="Proj. Máx." />
            )}
            {showProjection && (
              <Area dataKey="projMin" fill="#0f172a" stroke="none" name="Proj. Mín." />
            )}

            {/* Linha de tendência */}
            {showTrend && (
              <Line dataKey="trend" stroke="#facc15" strokeWidth={1.5} strokeDasharray="6 3"
                dot={false} name="Tendência (regressão)" connectNulls={false} />
            )}

            {/* Média móvel */}
            {showMA && (
              <Line dataKey="movingAvg" stroke="#a78bfa" strokeWidth={1.5}
                dot={false} name="Média Móvel (3d)" connectNulls={false} />
            )}

            {/* Linha principal do sensor */}
            <Line dataKey="value" stroke="#06b6d4" strokeWidth={2.5}
              dot={{ r: 3, fill: "#06b6d4", strokeWidth: 0 }} activeDot={{ r: 5 }}
              name="Sensor S-01" connectNulls={false} />

            {/* Projeção central */}
            {showProjection && (
              <Line dataKey="projMid" stroke="#fb923c" strokeWidth={2} strokeDasharray="4 4"
                dot={{ r: 3, fill: "#fb923c", strokeWidth: 0 }} name="Projeção (central)" connectNulls={false} />
            )}
          </ComposedChart>
        </ResponsiveContainer>

        {/* Legenda manual */}
        <div style={{ display: "flex", gap: 20, marginTop: 12, flexWrap: "wrap", justifyContent: "center" }}>
          {[
            { color: "#06b6d4", label: "Sensor S-01", solid: true },
            showTrend && { color: "#facc15", label: "Tendência (regressão linear)", dashed: true },
            showMA && { color: "#a78bfa", label: "Média Móvel (3d)", solid: true },
            showProjection && { color: "#fb923c", label: "Projeção futura", dashed: true },
          ].filter(Boolean).map((item, i) => (
            <div key={i} style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 11, color: "#94a3b8" }}>
              <div style={{
                width: 24, height: 2.5, background: item.dashed
                  ? `repeating-linear-gradient(to right, ${item.color} 0, ${item.color} 5px, transparent 5px, transparent 9px)`
                  : item.color,
                borderRadius: 2
              }} />
              {item.label}
            </div>
          ))}
        </div>
      </div>

      {/* ── Painel de análise ── */}
      <div style={{ background: "#1e293b", border: "1px solid #334155", borderRadius: 12, padding: "16px 20px" }}>
        <div style={{ fontWeight: 600, fontSize: 14, marginBottom: 12, color: "#f1f5f9" }}>📊 Análise de Tendência</div>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: 12 }}>

          <div style={{ background: "#0f172a", borderRadius: 8, padding: "12px 16px", borderLeft: `3px solid ${Math.abs(slope) < 0.3 ? "#3b82f6" : slope > 0 ? "#22c55e" : "#ef4444"}` }}>
            <div style={{ fontSize: 11, color: "#64748b", marginBottom: 6 }}>DIREÇÃO DA TENDÊNCIA</div>
            <TrendBadge slope={slope} />
            <div style={{ fontSize: 11, color: "#64748b", marginTop: 8 }}>
              Coef. angular: <strong style={{ color: "#e2e8f0" }}>{slope.toFixed(3)} %/dia</strong>
            </div>
          </div>

          <div style={{ background: "#0f172a", borderRadius: 8, padding: "12px 16px", borderLeft: "3px solid #facc15" }}>
            <div style={{ fontSize: 11, color: "#64748b", marginBottom: 6 }}>PROJEÇÃO (5 DIAS)</div>
            <div style={{ fontSize: 20, fontWeight: 700, color: "#facc15" }}>{proj5}%</div>
            <div style={{ fontSize: 11, color: "#64748b", marginTop: 4 }}>
              Margem de confiança: <strong style={{ color: "#e2e8f0" }}>± {Math.min(projectionDays * 2.5, 15).toFixed(0)}%</strong>
            </div>
          </div>

          <div style={{ background: "#0f172a", borderRadius: 8, padding: "12px 16px", borderLeft: `3px solid ${alertCount > 0 ? "#f87171" : "#4ade80"}` }}>
            <div style={{ fontSize: 11, color: "#64748b", marginBottom: 6 }}>CONFORMIDADE</div>
            <div style={{ fontSize: 20, fontWeight: 700, color: alertCount > 0 ? "#f87171" : "#4ade80" }}>
              {values.length ? (((values.length - alertCount) / values.length) * 100).toFixed(0) : 0}%
            </div>
            <div style={{ fontSize: 11, color: "#64748b", marginTop: 4 }}>
              {alertCount} leituras fora dos limites
            </div>
          </div>

          <div style={{ background: "#0f172a", borderRadius: 8, padding: "12px 16px", borderLeft: "3px solid #a78bfa" }}>
            <div style={{ fontSize: 11, color: "#64748b", marginBottom: 6 }}>VARIABILIDADE</div>
            <div style={{ fontSize: 20, fontWeight: 700, color: "#a78bfa" }}>
              {values.length ? (Math.max(...values) - Math.min(...values)).toFixed(1) : 0}%
            </div>
            <div style={{ fontSize: 11, color: "#64748b", marginTop: 4 }}>
              Amplitude máx. no período
            </div>
          </div>

        </div>
      </div>

      <div style={{ textAlign: "center", marginTop: 16, fontSize: 11, color: "#334155" }}>
        AgripeWeb · Mockup de Análise de Tendência · Dados simulados para visualização
      </div>
    </div>
  );
}
