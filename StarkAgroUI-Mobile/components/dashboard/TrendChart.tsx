import React from 'react';
import { View, Text, Dimensions } from 'react-native';
import Svg, { Polyline, Line, Polygon, Path, Text as SvgText } from 'react-native-svg';
import { TrendPoint, ProjectionPoint } from '../../services/trendAnalysis';
import { Colors } from '../../constants/colors';

const { width } = Dimensions.get('window');

const PADDING_LEFT = 36;
const PADDING_RIGHT = 8;
const PADDING_TOP = 8;
const PADDING_BOTTOM = 24;
const Y_LABELS = [100, 75, 50, 25, 0];

interface TrendChartProps {
  points: TrendPoint[];
  projection: ProjectionPoint[];
  humidityUpper: number;
  humidityLower: number;
  showTrend: boolean;
  showMovingAvg: boolean;
  showProjection: boolean;
  overrideWidth?: number;
  overrideHeight?: number;
}

export function TrendChart({
  points,
  projection,
  humidityUpper,
  humidityLower,
  showTrend,
  showMovingAvg,
  showProjection,
  overrideWidth,
  overrideHeight,
}: TrendChartProps) {
  const chartWidth = overrideWidth ?? width - 64;
  const chartHeight = overrideHeight ?? 200;
  const innerWidth = chartWidth - PADDING_LEFT - PADDING_RIGHT;
  const innerHeight = chartHeight - PADDING_TOP - PADDING_BOTTOM;

  const totalPoints = points.length + (showProjection ? projection.length : 0);

  if (points.length === 0) {
    return (
      <View style={{ height: chartHeight, justifyContent: 'center', alignItems: 'center' }}>
        <Text style={{ color: Colors.textSecondary }}>Sem dados disponíveis</Text>
      </View>
    );
  }

  function toX(index: number, total: number): number {
    return PADDING_LEFT + (index / Math.max(total - 1, 1)) * innerWidth;
  }

  function toY(value: number): number {
    return PADDING_TOP + innerHeight - (value / 100) * innerHeight;
  }

  // Build polyline points string
  function buildPoints(data: { x: number; y: number }[]): string {
    return data.map((p) => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(' ');
  }

  // Historical data line
  const dataCoords = points.map((p, i) => ({
    x: toX(i, totalPoints),
    y: toY(p.value),
  }));

  // Trend line
  const trendCoords = points
    .filter((p) => p.trend !== undefined)
    .map((p, i) => ({ x: toX(i, totalPoints), y: toY(p.trend!) }));

  // Moving average line
  const maCoords = points
    .filter((p) => p.movingAvg !== undefined)
    .map((p, i) => ({ x: toX(i, totalPoints), y: toY(p.movingAvg!) }));

  // Projection envelope polygon (top + bottom in reverse for closed shape)
  const projOffset = points.length;
  const projTopCoords = projection.map((p, i) => ({
    x: toX(projOffset + i, totalPoints),
    y: toY(Math.min(100, p.projMax)),
  }));
  const projBottomCoords = [...projection]
    .reverse()
    .map((p, i) => ({
      x: toX(projOffset + projection.length - 1 - i, totalPoints),
      y: toY(Math.max(0, p.projMin)),
    }));
  const projPolygonPoints = [...projTopCoords, ...projBottomCoords]
    .map((p) => `${p.x.toFixed(1)},${p.y.toFixed(1)}`)
    .join(' ');

  // Projection midline
  const projMidCoords = projection.map((p, i) => ({
    x: toX(projOffset + i, totalPoints),
    y: toY(p.projMid),
  }));

  // X-axis labels (first, middle, last of historical + first/last proj if shown)
  const xLabels: { label: string; x: number }[] = [];
  if (points.length > 0) {
    xLabels.push({ label: points[0].date, x: toX(0, totalPoints) });
    if (points.length > 2) {
      const mid = Math.floor((points.length - 1) / 2);
      xLabels.push({ label: points[mid].date, x: toX(mid, totalPoints) });
    }
    xLabels.push({ label: points[points.length - 1].date, x: toX(points.length - 1, totalPoints) });
  }
  if (showProjection && projection.length > 0) {
    xLabels.push({
      label: projection[projection.length - 1].date,
      x: toX(totalPoints - 1, totalPoints),
    });
  }

  // Reference lines Y positions
  const upperY = toY(humidityUpper);
  const lowerY = toY(humidityLower);

  return (
    <View>
      <Svg width={chartWidth} height={chartHeight}>
        {/* Y-axis labels */}
        {Y_LABELS.map((v) => (
          <SvgText
            key={v}
            x={PADDING_LEFT - 4}
            y={toY(v) + 3}
            fontSize={9}
            fill={Colors.textSecondary}
            textAnchor="end"
          >
            {v}
          </SvgText>
        ))}

        {/* Y grid lines */}
        {Y_LABELS.map((v) => (
          <Line
            key={`grid-${v}`}
            x1={PADDING_LEFT}
            y1={toY(v)}
            x2={chartWidth - PADDING_RIGHT}
            y2={toY(v)}
            stroke={Colors.cardBorder}
            strokeWidth={0.5}
          />
        ))}

        {/* Upper limit line */}
        <Line
          x1={PADDING_LEFT}
          y1={upperY}
          x2={chartWidth - PADDING_RIGHT}
          y2={upperY}
          stroke={Colors.limitUpper}
          strokeWidth={1}
          strokeDasharray="4,3"
        />

        {/* Lower limit line */}
        <Line
          x1={PADDING_LEFT}
          y1={lowerY}
          x2={chartWidth - PADDING_RIGHT}
          y2={lowerY}
          stroke={Colors.limitLower}
          strokeWidth={1}
          strokeDasharray="4,3"
        />

        {/* Projection envelope */}
        {showProjection && projection.length > 0 && (
          <>
            <Polygon
              points={projPolygonPoints}
              fill={Colors.projection}
              fillOpacity={0.15}
            />
            <Polyline
              points={buildPoints(projMidCoords)}
              fill="none"
              stroke={Colors.projection}
              strokeWidth={1.5}
              strokeDasharray="5,3"
            />
          </>
        )}

        {/* Trend line */}
        {showTrend && trendCoords.length > 1 && (
          <Polyline
            points={buildPoints(trendCoords)}
            fill="none"
            stroke={Colors.trendLine}
            strokeWidth={1.5}
            strokeDasharray="6,3"
          />
        )}

        {/* Moving average line */}
        {showMovingAvg && maCoords.length > 1 && (
          <Polyline
            points={buildPoints(maCoords)}
            fill="none"
            stroke={Colors.movingAvg}
            strokeWidth={1.5}
          />
        )}

        {/* Main data line */}
        {dataCoords.length > 1 && (
          <Polyline
            points={buildPoints(dataCoords)}
            fill="none"
            stroke={Colors.primary}
            strokeWidth={2}
          />
        )}

        {/* X-axis labels */}
        {xLabels.map((l, i) => (
          <SvgText
            key={i}
            x={l.x}
            y={chartHeight - 4}
            fontSize={9}
            fill={Colors.textSecondary}
            textAnchor="middle"
          >
            {l.label}
          </SvgText>
        ))}
      </Svg>

      {/* Legend */}
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 10, marginTop: 8 }}>
        <LegendItem color={Colors.primary} label="Leituras" />
        <LegendItem color={Colors.limitUpper} label={`L.Sup ${humidityUpper}%`} dashed />
        <LegendItem color={Colors.limitLower} label={`L.Inf ${humidityLower}%`} dashed />
        {showTrend && <LegendItem color={Colors.trendLine} label="Tendência" dashed />}
        {showMovingAvg && <LegendItem color={Colors.movingAvg} label="Méd.Móvel" />}
        {showProjection && <LegendItem color={Colors.projection} label="Projeção" dashed />}
      </View>
    </View>
  );
}

function LegendItem({ color, label, dashed }: { color: string; label: string; dashed?: boolean }) {
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center' }}>
      <View
        style={{
          width: 14,
          height: 2,
          backgroundColor: dashed ? 'transparent' : color,
          borderBottomWidth: dashed ? 2 : 0,
          borderBottomColor: color,
          borderStyle: dashed ? 'dashed' : 'solid',
          marginRight: 4,
        }}
      />
      <Text style={{ color: Colors.textSecondary, fontSize: 10 }}>{label}</Text>
    </View>
  );
}
