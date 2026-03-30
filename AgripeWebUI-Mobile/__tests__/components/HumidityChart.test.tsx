import React from 'react';
import { render } from '@testing-library/react-native';
import { HumidityChart } from '../../components/dashboard/HumidityChart';

const emptyChartData = [
  { quadrante: 1, readings: [] },
  { quadrante: 2, readings: [] },
  { quadrante: 3, readings: [] },
  { quadrante: 4, readings: [] },
];

const withData = [
  {
    quadrante: 1,
    readings: [
      { id: 1, sensorId: 10, value: 60, date: '2025-01-01' },
      { id: 2, sensorId: 10, value: 65, date: '2025-01-02' },
    ],
  },
  { quadrante: 2, readings: [] },
  { quadrante: 3, readings: [] },
  { quadrante: 4, readings: [] },
];

describe('HumidityChart', () => {
  it('renders "Sem dados" when all readings are empty', () => {
    const { getByText } = render(
      <HumidityChart chartData={emptyChartData} humidityUpper={75} humidityLower={25} />
    );
    expect(getByText('Sem dados para exibir')).toBeTruthy();
  });

  it('renders the chart area when data is present', () => {
    const { queryByText } = render(
      <HumidityChart chartData={withData} humidityUpper={75} humidityLower={25} />
    );
    expect(queryByText('Sem dados para exibir')).toBeNull();
  });

  it('shows upper and lower limit labels', () => {
    const { getByText } = render(
      <HumidityChart chartData={withData} humidityUpper={75} humidityLower={25} />
    );
    expect(getByText('75%')).toBeTruthy();
    expect(getByText('25%')).toBeTruthy();
  });

  it('renders legend items for all quadrants', () => {
    const { getByText } = render(
      <HumidityChart chartData={withData} humidityUpper={75} humidityLower={25} />
    );
    expect(getByText('Q1')).toBeTruthy();
    expect(getByText('Q2')).toBeTruthy();
    expect(getByText('Q3')).toBeTruthy();
    expect(getByText('Q4')).toBeTruthy();
  });
});
