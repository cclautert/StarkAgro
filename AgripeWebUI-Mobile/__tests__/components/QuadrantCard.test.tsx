import React from 'react';
import { render } from '@testing-library/react-native';
import { QuadrantCard } from '../../components/dashboard/QuadrantCard';

const defaultProps = {
  quadranteNumber: 1,
  avg: null as number | null,
  humidityUpper: 75,
  humidityLower: 25,
  onPress: jest.fn(),
};

describe('QuadrantCard', () => {
  it('shows "Sem dados" when avg is null', () => {
    const { getByText } = render(<QuadrantCard {...defaultProps} avg={null} />);
    expect(getByText('Sem dados')).toBeTruthy();
  });

  it('shows low humidity alert when avg < lower', () => {
    const { getByText } = render(<QuadrantCard {...defaultProps} avg={10} />);
    expect(getByText(/Umidade Baixa/)).toBeTruthy();
  });

  it('shows high humidity alert when avg > upper', () => {
    const { getByText } = render(<QuadrantCard {...defaultProps} avg={90} />);
    expect(getByText(/Umidade Alta/)).toBeTruthy();
  });

  it('shows "Normal" when avg is just above lower (< lower + 15)', () => {
    // lower=25, lower+15=40; avg=30 falls in Normal range
    const { getByText } = render(<QuadrantCard {...defaultProps} avg={30} />);
    expect(getByText('Normal')).toBeTruthy();
  });

  it('shows "Ótimo" when avg is well within range', () => {
    // avg=60 is between 25 and 75, and >= 25+15=40
    const { getByText } = render(<QuadrantCard {...defaultProps} avg={60} />);
    expect(getByText('Ótimo')).toBeTruthy();
  });

  it('displays the avg value', () => {
    const { getByText } = render(<QuadrantCard {...defaultProps} avg={55.5} />);
    expect(getByText('55.5')).toBeTruthy();
  });

  it('displays em dash when avg is null', () => {
    const { getByText } = render(<QuadrantCard {...defaultProps} avg={null} />);
    expect(getByText('—')).toBeTruthy();
  });
});
