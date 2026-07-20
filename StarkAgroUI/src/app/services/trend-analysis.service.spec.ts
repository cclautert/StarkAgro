import { TestBed } from '@angular/core/testing';
import { TrendAnalysisService, TrendPoint } from './trend-analysis.service';
import { Read } from '../models/read.model';

// Helper: build a Read with a given value and ISO date string
function makeRead(value: number, dateIso: string): Read {
  return { sensorId: 1, value, date: new Date(dateIso) };
}

// Helper: build a TrendPoint array from plain values (date is unused by the algorithms)
function makeTrendPoints(values: number[]): TrendPoint[] {
  return values.map((v, i) => ({ date: `0${i + 1}/01`, value: v }));
}

describe('TrendAnalysisService', () => {
  let service: TrendAnalysisService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(TrendAnalysisService);
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  // ── linearRegression ───────────────────────────────────────────────────────

  describe('linearRegression', () => {
    it('with flat data returns slope ~0 and intercept equal to the value', () => {
      const data = makeTrendPoints([50, 50, 50, 50, 50]);
      const { slope, intercept } = service.linearRegression(data);
      expect(Math.abs(slope)).toBeLessThan(0.001);
      expect(intercept).toBeCloseTo(50, 1);
    });

    it('with strictly increasing data [1,2,3,4,5] returns slope ≈ 1 and intercept ≈ 1', () => {
      const data = makeTrendPoints([1, 2, 3, 4, 5]);
      const { slope, intercept } = service.linearRegression(data);
      expect(slope).toBeCloseTo(1, 2);
      expect(intercept).toBeCloseTo(1, 2);
    });

    it('with a single point returns slope=0 and intercept equal to that value', () => {
      const data = makeTrendPoints([42]);
      const { slope, intercept } = service.linearRegression(data);
      expect(slope).toBe(0);
      expect(intercept).toBe(42);
    });

    it('with an empty array returns slope=0 and intercept=0', () => {
      const { slope, intercept } = service.linearRegression([]);
      expect(slope).toBe(0);
      expect(intercept).toBe(0);
    });
  });

  // ── movingAverage ──────────────────────────────────────────────────────────

  describe('movingAverage', () => {
    it('window=3 at index 2 equals average of indices 0, 1 and 2', () => {
      const points = makeTrendPoints([10, 20, 30, 40, 50]);
      const result = service.movingAverage(points, 3);
      // index 2: slice [10, 20, 30] → avg = 20
      expect(result[2].movingAvg).toBeCloseTo(20, 1);
    });

    it('window=3 at index 0 returns the single available value', () => {
      const points = makeTrendPoints([10, 20, 30]);
      const result = service.movingAverage(points, 3);
      expect(result[0].movingAvg).toBeCloseTo(10, 1);
    });

    it('window=1 returns original values unchanged', () => {
      const values = [5, 15, 25, 35];
      const points = makeTrendPoints(values);
      const result = service.movingAverage(points, 1);
      result.forEach((p, i) => {
        expect(p.movingAvg).toBeCloseTo(values[i], 1);
      });
    });

    it('does not mutate the original data array', () => {
      const points = makeTrendPoints([10, 20, 30]);
      const copy = points.map(p => ({ ...p }));
      service.movingAverage(points, 3);
      points.forEach((p, i) => {
        expect(p.value).toBe(copy[i].value);
      });
    });

    it('window=3 at index 1 uses a partial window of 2 values', () => {
      // index 1: slice [10, 20] → avg = 15
      const points = makeTrendPoints([10, 20, 30, 40]);
      const result = service.movingAverage(points, 3);
      expect(result[1].movingAvg).toBeCloseTo(15, 1);
    });

    it('window=3 at index 3 covers exactly the last 3 values', () => {
      // index 3: slice [20, 30, 40] → avg = 30
      const points = makeTrendPoints([10, 20, 30, 40]);
      const result = service.movingAverage(points, 3);
      expect(result[3].movingAvg).toBeCloseTo(30, 1);
    });
  });

  // ── buildProjection ────────────────────────────────────────────────────────

  describe('buildProjection', () => {
    it('returns exactly projDays entries when projDays=5', () => {
      const result = service.buildProjection(1, 50, 10, 5);
      expect(result.length).toBe(5);
    });

    it('returns 0 entries when projDays=0', () => {
      const result = service.buildProjection(1, 50, 10, 0);
      expect(result.length).toBe(0);
    });

    it('dates are labelled +1d through +5d', () => {
      const result = service.buildProjection(0, 60, 7, 5);
      expect(result.map(r => r.date)).toEqual(['+1d', '+2d', '+3d', '+4d', '+5d']);
    });

    it('margin grows with projection distance: entry[4] has a wider band than entry[0]', () => {
      const result = service.buildProjection(0, 60, 7, 5);
      const margin0 = result[0].projMax - result[0].projMid;
      const margin4 = result[4].projMax - result[4].projMid;
      expect(margin4).toBeGreaterThan(margin0);
    });

    it('margin formula is Math.min(p * 2.5, 15) — capped at 15 for day 6+', () => {
      // Day 6 would give 15 (cap), day 5 gives 12.5
      const result = service.buildProjection(0, 60, 10, 6);
      expect(result[4].projMax - result[4].projMid).toBeCloseTo(12.5, 1); // p=5
      expect(result[5].projMax - result[5].projMid).toBeCloseTo(15, 1);   // p=6 capped
    });

    it('clamps projected value to [0, 100]', () => {
      // slope=5, intercept=98, n=10: day 1 projection = 98 + 5*10 = 148 → clamped to 100
      const result = service.buildProjection(5, 98, 10, 1);
      expect(result[0].projMid).toBeLessThanOrEqual(100);
      // slope=-5, intercept=2, n=10: day 1 projection = 2 + (-5)*10 = -48 → clamped to 0
      const result2 = service.buildProjection(-5, 2, 10, 1);
      expect(result2[0].projMid).toBeGreaterThanOrEqual(0);
    });

    it('projMid is always between projMin and projMax', () => {
      const result = service.buildProjection(1.2, 55, 14, 5);
      for (const p of result) {
        expect(p.projMid).toBeGreaterThanOrEqual(p.projMin);
        expect(p.projMid).toBeLessThanOrEqual(p.projMax);
      }
    });

    it('projMin may go below 0 when margin exceeds the clamped value', () => {
      // slope=0, intercept=0 → projMid clamped to 0 for all days; margin for p=1 is 2.5
      // so projMin = 0 - 2.5 = -2.5 (not further clamped — only projMid is clamped)
      const result = service.buildProjection(0, 0, 10, 1);
      expect(result[0].projMid).toBe(0);
      expect(result[0].projMin).toBeCloseTo(-2.5, 1);
    });

    it('exact projMid values match the formula intercept + slope * (n - 1 + p)', () => {
      // slope=2, intercept=10, n=5
      // p=1: rawValue = 10 + 2 * (4 + 1) = 20, within [0,100] → projMid = 20
      // p=2: rawValue = 10 + 2 * (4 + 2) = 22 → projMid = 22
      const result = service.buildProjection(2, 10, 5, 2);
      expect(result[0].projMid).toBeCloseTo(20, 1);
      expect(result[1].projMid).toBeCloseTo(22, 1);
    });
  });

  // ── computeDailyData ────────────────────────────────────────────────────────

  describe('computeDailyData', () => {
    it('groups two readings on the same day into one point and takes the last reading', () => {
      const reads: Read[] = [
        makeRead(55, '2024-01-10T08:00:00Z'),  // earlier on day 10
        makeRead(70, '2024-01-10T20:00:00Z'),  // later on day 10 — must win
      ];
      const { points } = service.computeDailyData(reads, 40, 80);
      expect(points.length).toBe(1);
      expect(points[0].value).toBe(70);
    });

    it('groups readings on different days into separate points', () => {
      const reads: Read[] = [
        makeRead(55, '2024-01-10T12:00:00Z'),
        makeRead(60, '2024-01-11T12:00:00Z'),
        makeRead(65, '2024-01-12T12:00:00Z'),
      ];
      const { points } = service.computeDailyData(reads, 40, 80);
      expect(points.length).toBe(3);
    });

    it('returns empty result when reads array is empty', () => {
      const { points, projection, stats } = service.computeDailyData([], 40, 80);
      expect(points.length).toBe(0);
      expect(projection.length).toBe(0);
      expect(stats.alertCount).toBe(0);
    });

    it('computes alertCount correctly — 2 readings outside [40, 80]', () => {
      const reads: Read[] = [
        makeRead(30, '2024-01-01T12:00:00Z'),  // below 40 → alert
        makeRead(50, '2024-01-02T12:00:00Z'),
        makeRead(60, '2024-01-03T12:00:00Z'),
        makeRead(70, '2024-01-04T12:00:00Z'),
        makeRead(75, '2024-01-05T12:00:00Z'),
        makeRead(55, '2024-01-06T12:00:00Z'),
        makeRead(45, '2024-01-07T12:00:00Z'),
        makeRead(65, '2024-01-08T12:00:00Z'),
        makeRead(60, '2024-01-09T12:00:00Z'),
        makeRead(90, '2024-01-10T12:00:00Z'),  // above 80 → alert
      ];
      const { stats } = service.computeDailyData(reads, 40, 80);
      expect(stats.alertCount).toBe(2);
    });

    it('computes compliancePct as (10-2)/10 = 80% when 2 alerts out of 10 readings', () => {
      const reads: Read[] = [
        makeRead(30, '2024-01-01T12:00:00Z'),  // alert
        makeRead(50, '2024-01-02T12:00:00Z'),
        makeRead(60, '2024-01-03T12:00:00Z'),
        makeRead(70, '2024-01-04T12:00:00Z'),
        makeRead(75, '2024-01-05T12:00:00Z'),
        makeRead(55, '2024-01-06T12:00:00Z'),
        makeRead(45, '2024-01-07T12:00:00Z'),
        makeRead(65, '2024-01-08T12:00:00Z'),
        makeRead(60, '2024-01-09T12:00:00Z'),
        makeRead(90, '2024-01-10T12:00:00Z'),  // alert
      ];
      const { stats } = service.computeDailyData(reads, 40, 80);
      expect(stats.compliancePct).toBe(80);
    });

    it('computes variability as max - min of all raw values', () => {
      const reads: Read[] = [
        makeRead(20, '2024-01-01T12:00:00Z'),
        makeRead(80, '2024-01-02T12:00:00Z'),
        makeRead(50, '2024-01-03T12:00:00Z'),
      ];
      const { stats } = service.computeDailyData(reads, 40, 80);
      expect(stats.variability).toBeCloseTo(60, 1);
    });

    it('always returns exactly 5 projection points', () => {
      const reads: Read[] = [
        makeRead(55, '2024-01-10T12:00:00Z'),
        makeRead(60, '2024-01-11T12:00:00Z'),
        makeRead(65, '2024-01-12T12:00:00Z'),
      ];
      const { projection } = service.computeDailyData(reads, 40, 80);
      expect(projection.length).toBe(5);
    });

    it('single reading produces slope=0 and movingAvg equal to that value', () => {
      const reads: Read[] = [makeRead(55, '2024-01-10T12:00:00Z')];
      const { points, stats } = service.computeDailyData(reads, 40, 80);
      expect(stats.slope).toBe(0);
      expect(points[0].movingAvg).toBeCloseTo(55, 1);
    });

    it('proj5 is clamped to [0, 100]', () => {
      // Force a very steep positive slope so projection would exceed 100
      const reads: Read[] = Array.from({ length: 10 }, (_, i) =>
        makeRead(80 + i * 5, `2024-01-${(i + 1).toString().padStart(2, '0')}T12:00:00Z`)
      );
      const { stats } = service.computeDailyData(reads, 40, 80);
      expect(stats.proj5).toBeLessThanOrEqual(100);
      expect(stats.proj5).toBeGreaterThanOrEqual(0);
    });

    // ── Additional tests filling gaps identified during review ─────────────

    it('variability uses all raw reads — intra-day outlier is included in range', () => {
      // Day 1 has two readings: 10 (outlier) and 90 (last, kept for the daily point).
      // Day 2 has one reading: 50.
      // All raw values are [10, 90, 50] → variability = 90 - 10 = 80.
      // If variability were computed only from daily point values ([90, 50]) it would be 40.
      const reads: Read[] = [
        makeRead(10, '2024-01-01T06:00:00Z'),  // outlier — earlier reading, not the daily value
        makeRead(90, '2024-01-01T18:00:00Z'),  // last reading of day 1 → daily point value = 90
        makeRead(50, '2024-01-02T12:00:00Z'),
      ];
      const { stats } = service.computeDailyData(reads, 40, 80);
      expect(stats.variability).toBeCloseTo(80, 1);
    });

    it('avg uses all raw reads — intra-day readings all contribute to the mean', () => {
      // Day 1: two readings 10 and 90 (daily point = 90); Day 2: one reading 50.
      // Raw avg = (10 + 90 + 50) / 3 = 50.
      // Daily-point avg = (90 + 50) / 2 = 70. The service must return 50.
      const reads: Read[] = [
        makeRead(10, '2024-01-01T06:00:00Z'),
        makeRead(90, '2024-01-01T18:00:00Z'),
        makeRead(50, '2024-01-02T12:00:00Z'),
      ];
      const { stats } = service.computeDailyData(reads, 40, 80);
      expect(stats.avg).toBeCloseTo(50, 0);
    });

    it('alertCount counts all raw reads that breach limits — not just daily points', () => {
      // Day 1: two readings — 20 (breaches limiteInferior=40) and 60 (in range, daily point).
      // Day 2: one reading — 85 (breaches limiteSuperior=80).
      // alertCount over raw reads = 2.
      const reads: Read[] = [
        makeRead(20, '2024-01-01T06:00:00Z'),  // breach — not the daily point
        makeRead(60, '2024-01-01T18:00:00Z'),  // in range — becomes daily point
        makeRead(85, '2024-01-02T12:00:00Z'),  // breach
      ];
      const { stats } = service.computeDailyData(reads, 40, 80);
      expect(stats.alertCount).toBe(2);
    });

    it('stats.last equals the value of the final daily group', () => {
      const reads: Read[] = [
        makeRead(55, '2024-01-10T12:00:00Z'),
        makeRead(62, '2024-01-11T08:00:00Z'),
        makeRead(71, '2024-01-11T22:00:00Z'),  // last reading of day 11 — must be stats.last
      ];
      const { stats } = service.computeDailyData(reads, 40, 80);
      expect(stats.last).toBe(71);
    });

    it('each TrendPoint has a numeric trend field populated after computation', () => {
      const reads: Read[] = [
        makeRead(40, '2024-01-01T12:00:00Z'),
        makeRead(50, '2024-01-02T12:00:00Z'),
        makeRead(60, '2024-01-03T12:00:00Z'),
      ];
      const { points } = service.computeDailyData(reads, 30, 70);
      for (const p of points) {
        expect(typeof p.trend).toBe('number');
        expect(isFinite(p.trend!)).toBeTrue();
      }
    });

    it('day label on returned TrendPoints uses DD/MM format', () => {
      const reads: Read[] = [
        makeRead(55, '2024-03-05T12:00:00Z'),
        makeRead(60, '2024-03-06T12:00:00Z'),
      ];
      const { points } = service.computeDailyData(reads, 40, 80);
      expect(points[0].date).toBe('05/03');
      expect(points[1].date).toBe('06/03');
    });

    it('empty stats have compliancePct=100 as a safe default', () => {
      const { stats } = service.computeDailyData([], 40, 80);
      expect(stats.compliancePct).toBe(100);
    });
  });

  // ── toDayLabel ──────────────────────────────────────────────────────────────

  describe('toDayLabel', () => {
    it('formats a UTC date as DD/MM', () => {
      const d = new Date('2024-03-05T00:00:00Z');
      expect(service.toDayLabel(d)).toBe('05/03');
    });

    it('pads single-digit day and month with leading zeros', () => {
      const d = new Date('2024-01-07T12:00:00Z');
      expect(service.toDayLabel(d)).toBe('07/01');
    });

    it('uses UTC calendar day — a reading at 23:59:59Z stays on its UTC date', () => {
      // 2024-01-15T23:59:59Z is still January 15 in UTC, regardless of local timezone
      const d = new Date('2024-01-15T23:59:59Z');
      expect(service.toDayLabel(d)).toBe('15/01');
    });

    it('uses UTC calendar day — a reading at 00:00:01Z on day 16 is day 16, not 15', () => {
      const d = new Date('2024-01-16T00:00:01Z');
      expect(service.toDayLabel(d)).toBe('16/01');
    });

    it('handles end-of-month day correctly', () => {
      const d = new Date('2024-01-31T12:00:00Z');
      expect(service.toDayLabel(d)).toBe('31/01');
    });

    it('handles December correctly', () => {
      const d = new Date('2024-12-25T12:00:00Z');
      expect(service.toDayLabel(d)).toBe('25/12');
    });
  });
});
