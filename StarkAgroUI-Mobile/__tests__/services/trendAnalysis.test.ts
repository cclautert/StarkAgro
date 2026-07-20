import {
  toDayLabel,
  linearRegression,
  movingAverage,
  buildProjection,
  computeDailyData,
  TrendPoint,
} from '../../services/trendAnalysis';
import { ReadEntry } from '../../types/api';

// Código de análise puro — nenhum mock. É a parte que projeta a umidade dos próximos dias,
// então os limites (regressão degenerada, clamp em 0–100, dia único) importam de verdade.

describe('toDayLabel', () => {
  it('formata dia/mês com zero à esquerda', () => {
    expect(toDayLabel(new Date('2026-03-07T12:00:00'))).toBe('07/03');
    expect(toDayLabel(new Date('2026-11-25T00:00:00'))).toBe('25/11');
  });
});

describe('linearRegression', () => {
  it('devolve slope 0 e o próprio valor com menos de 2 pontos', () => {
    expect(linearRegression([])).toEqual({ slope: 0, intercept: 0 });
    expect(linearRegression([{ value: 42 }])).toEqual({ slope: 0, intercept: 42 });
  });

  it('acha a reta de uma série perfeitamente linear', () => {
    const { slope, intercept } = linearRegression([
      { value: 10 }, { value: 20 }, { value: 30 }, { value: 40 },
    ]);
    expect(slope).toBeCloseTo(10, 3);
    expect(intercept).toBeCloseTo(10, 3);
  });

  it('slope negativo quando a série cai — é o caso que dispara o alerta de secagem', () => {
    const { slope } = linearRegression([{ value: 80 }, { value: 60 }, { value: 40 }]);
    expect(slope).toBeLessThan(0);
  });

  it('slope 0 quando todos os pontos são iguais (denominador não-zero, valor constante)', () => {
    const { slope, intercept } = linearRegression([
      { value: 50 }, { value: 50 }, { value: 50 },
    ]);
    expect(slope).toBe(0);
    expect(intercept).toBeCloseTo(50, 3);
  });
});

describe('movingAverage', () => {
  it('a média móvel do primeiro ponto é o próprio valor', () => {
    const data: TrendPoint[] = [
      { date: '01/01', value: 10 },
      { date: '02/01', value: 20 },
      { date: '03/01', value: 30 },
    ];
    const out = movingAverage(data, 3);
    expect(out[0].movingAvg).toBe(10);
    expect(out[2].movingAvg).toBe(20); // (10+20+30)/3
  });

  it('a janela não olha além dos pontos disponíveis', () => {
    const data: TrendPoint[] = [
      { date: '01/01', value: 0 },
      { date: '02/01', value: 100 },
    ];
    const out = movingAverage(data, 5);
    expect(out[1].movingAvg).toBe(50);
  });
});

describe('buildProjection', () => {
  it('gera projDays pontos com margem crescente até o teto de 15', () => {
    const proj = buildProjection(1, 50, 5, 5);
    expect(proj).toHaveLength(5);
    expect(proj[0].date).toBe('+1d');
    // margem = min(p*2.5, 15): no 6º dia+ satura, mas aqui p=5 => 12.5
    expect(proj[4].projMax - proj[4].projMid).toBeCloseTo(12.5, 1);
  });

  it('faz clamp entre 0 e 100 (umidade não passa disso)', () => {
    const acima = buildProjection(50, 90, 1, 1);
    expect(acima[0].projMid).toBe(100);
    const abaixo = buildProjection(-50, 10, 1, 1);
    expect(abaixo[0].projMid).toBe(0);
  });

  it('satura a margem em 15 a partir do 6º dia', () => {
    const proj = buildProjection(0, 50, 1, 7);
    expect(proj[6].projMax - proj[6].projMid).toBeCloseTo(15, 1);
  });
});

describe('computeDailyData', () => {
  const read = (date: string, value: number): ReadEntry =>
    ({ date, value } as ReadEntry);

  it('série vazia devolve stats zeradas e 100% de conformidade', () => {
    const { points, projection, stats } = computeDailyData([], 30, 80);
    expect(points).toEqual([]);
    expect(projection).toEqual([]);
    expect(stats.compliancePct).toBe(100);
    expect(stats.alertCount).toBe(0);
  });

  it('null como leitura também é tratado como vazio', () => {
    const { stats } = computeDailyData(null as unknown as ReadEntry[], 30, 80);
    expect(stats.compliancePct).toBe(100);
  });

  it('agrupa por dia mantendo a última leitura de cada dia', () => {
    const reads = [
      read('2026-03-01T08:00:00Z', 40),
      read('2026-03-01T18:00:00Z', 60), // vence no dia 01
      read('2026-03-02T09:00:00Z', 55),
    ];
    const { points } = computeDailyData(reads, 30, 80);
    expect(points).toHaveLength(2);
    expect(points[0].value).toBe(60);
  });

  it('conta alertas fora da faixa e calcula conformidade sobre leituras cruas', () => {
    const reads = [
      read('2026-03-01T08:00:00Z', 20), // < 30 → alerta
      read('2026-03-02T08:00:00Z', 50),
      read('2026-03-03T08:00:00Z', 90), // > 80 → alerta
      read('2026-03-04T08:00:00Z', 60),
    ];
    const { stats } = computeDailyData(reads, 30, 80);
    expect(stats.alertCount).toBe(2);
    expect(stats.compliancePct).toBe(50);
    expect(stats.min).toBe(20);
    expect(stats.max).toBe(90);
    expect(stats.variability).toBe(70);
  });

  it('projeta 5 dias e expõe proj5', () => {
    const reads = Array.from({ length: 6 }, (_, i) =>
      read(`2026-03-0${i + 1}T08:00:00Z`, 70 - i * 5)
    );
    const { projection, stats } = computeDailyData(reads, 30, 80);
    expect(projection).toHaveLength(5);
    expect(typeof stats.proj5).toBe('number');
    expect(stats.slope).toBeLessThan(0); // série decrescente
  });
});
