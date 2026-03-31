import React, { useMemo } from 'react';
import { View, Text, Dimensions } from 'react-native';
import { ReadEntry } from '../../types/api';
import { Colors } from '../../constants/colors';

const { width } = Dimensions.get('window');

const QUADRANT_COLORS = [Colors.q1, Colors.q2, Colors.q3, Colors.q4];
const QUADRANT_NAMES = ['Q1', 'Q2', 'Q3', 'Q4'];

interface HumidityChartProps {
  chartData: Array<{ quadrante: number; readings: ReadEntry[] }>;
  humidityUpper: number;
  humidityLower: number;
}

function formatDateShort(dateStr: string): string {
  const d = new Date(dateStr);
  const day = d.getDate().toString().padStart(2, '0');
  const month = (d.getMonth() + 1).toString().padStart(2, '0');
  return `${day}/${month}`;
}

export function HumidityChart({ chartData, humidityUpper, humidityLower }: HumidityChartProps) {
  const chartWidth = width - 64;
  const chartHeight = 180;
  const paddingLeft = 32;
  const paddingBottom = 20;
  const innerWidth = chartWidth - paddingLeft;
  const innerHeight = chartHeight - paddingBottom;

  const aggregatedData = useMemo(() => {
    return chartData.map((qd) => {
      const byDay = new Map<string, ReadEntry>();
      for (const r of qd.readings) {
        const key = formatDateShort(r.date as string);
        byDay.set(key, r);
      }
      return { quadrante: qd.quadrante, readings: Array.from(byDay.values()) };
    });
  }, [chartData]);

  const minVal = 0;
  const maxVal = 100;
  const range = 100;

  function toY(val: number): number {
    return innerHeight - ((val - minVal) / range) * innerHeight;
  }

  function toX(index: number, total: number): number {
    return paddingLeft + (index / Math.max(total - 1, 1)) * innerWidth;
  }

  // Reference line Y positions
  const upperY = toY(humidityUpper);
  const lowerY = toY(humidityLower);

  // Build SVG-less chart using View bars per quadrant
  // We'll render a simple layered line chart using absolute positioned Views
  const hasData = aggregatedData.some((d) => d.readings.length > 0);

  if (!hasData) {
    return (
      <View style={{ height: chartHeight, justifyContent: 'center', alignItems: 'center' }}>
        <Text style={{ color: Colors.textSecondary }}>Sem dados para exibir</Text>
      </View>
    );
  }

  return (
    <View style={{ marginTop: 8 }}>
      {/* Y-axis labels */}
      <View style={{ flexDirection: 'row' }}>
        {/* Y labels */}
        <View style={{ width: paddingLeft, height: innerHeight, justifyContent: 'space-between', alignItems: 'flex-end', paddingRight: 4 }}>
          <Text style={{ color: Colors.textSecondary, fontSize: 9 }}>{maxVal.toFixed(0)}</Text>
          <Text style={{ color: Colors.textSecondary, fontSize: 9 }}>{((maxVal + minVal) / 2).toFixed(0)}</Text>
          <Text style={{ color: Colors.textSecondary, fontSize: 9 }}>{minVal.toFixed(0)}</Text>
        </View>

        {/* Chart area */}
        <View style={{ flex: 1, height: innerHeight, position: 'relative' }}>
          {/* Upper limit line */}
          <View
            style={{
              position: 'absolute',
              left: 0,
              right: 0,
              top: upperY,
              height: 1,
              backgroundColor: Colors.limitUpper,
              borderStyle: 'dashed',
            }}
          />
          <Text
            style={{
              position: 'absolute',
              right: 0,
              top: upperY - 10,
              color: Colors.limitUpper,
              fontSize: 9,
            }}
          >
            {humidityUpper}%
          </Text>

          {/* Lower limit line */}
          <View
            style={{
              position: 'absolute',
              left: 0,
              right: 0,
              top: lowerY,
              height: 1,
              backgroundColor: Colors.limitLower,
            }}
          />
          <Text
            style={{
              position: 'absolute',
              right: 0,
              top: lowerY - 10,
              color: Colors.limitLower,
              fontSize: 9,
            }}
          >
            {humidityLower}%
          </Text>

          {/* Quadrant bar charts stacked */}
          {aggregatedData.map((qd, qi) => {
            if (!qd.readings.length) return null;
            const color = QUADRANT_COLORS[qi] ?? Colors.primary;
            return (
              <View
                key={qd.quadrante}
                style={{ position: 'absolute', left: 0, right: 0, top: 0, bottom: 0, flexDirection: 'row', alignItems: 'flex-end' }}
              >
                {qd.readings.map((r, ri) => {
                  const barH = ((r.value - minVal) / range) * innerHeight;
                  return (
                    <View
                      key={ri}
                      style={{
                        flex: 1,
                        height: Math.max(barH, 1),
                        backgroundColor: color,
                        opacity: 0.3 + qi * 0.15,
                        marginHorizontal: 0.5,
                      }}
                    />
                  );
                })}
              </View>
            );
          })}
        </View>
      </View>

      {/* X-axis labels */}
      <View style={{ flexDirection: 'row', marginLeft: paddingLeft, marginTop: 4 }}>
        {(() => {
          const firstQ = aggregatedData.find((d) => d.readings.length > 0);
          if (!firstQ) return null;
          const r = firstQ.readings;
          return (
            <>
              <Text style={{ color: Colors.textSecondary, fontSize: 9 }}>
                {r[0] ? formatDateShort(r[0].date as string) : ''}
              </Text>
              <View style={{ flex: 1 }} />
              <Text style={{ color: Colors.textSecondary, fontSize: 9 }}>
                {r[r.length - 1] ? formatDateShort(r[r.length - 1].date as string) : ''}
              </Text>
            </>
          );
        })()}
      </View>

      {/* Legend */}
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', marginTop: 12, gap: 8 }}>
        {QUADRANT_NAMES.map((name, i) => (
          <View key={name} style={{ flexDirection: 'row', alignItems: 'center' }}>
            <View style={{ width: 12, height: 3, backgroundColor: QUADRANT_COLORS[i], borderRadius: 2, marginRight: 4 }} />
            <Text style={{ color: Colors.textSecondary, fontSize: 11 }}>{name}</Text>
          </View>
        ))}
        <View style={{ flexDirection: 'row', alignItems: 'center' }}>
          <View style={{ width: 12, height: 2, backgroundColor: Colors.limitUpper, marginRight: 4 }} />
          <Text style={{ color: Colors.textSecondary, fontSize: 11 }}>L.Sup</Text>
        </View>
        <View style={{ flexDirection: 'row', alignItems: 'center' }}>
          <View style={{ width: 12, height: 2, backgroundColor: Colors.limitLower, marginRight: 4 }} />
          <Text style={{ color: Colors.textSecondary, fontSize: 11 }}>L.Inf</Text>
        </View>
      </View>
    </View>
  );
}
