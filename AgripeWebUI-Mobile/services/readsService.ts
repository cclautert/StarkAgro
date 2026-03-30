import api from './api';
import { GetReadByPivotIdResponse, ReadEntry } from '../types/api';

export const readsService = {
  /** Returns pivot with quadrant averages. numberOfReads = number of past days. */
  async getByPivotId(
    pivotId: number,
    numberOfReads: number = 7
  ): Promise<GetReadByPivotIdResponse> {
    const res = await api.get<GetReadByPivotIdResponse>('reads/GetByPivotId', {
      params: { PivotId: pivotId, NumberOfReads: numberOfReads },
    });
    return res.data;
  },

  /** Returns time-series readings for a specific sensor/quadrant. numberOfReads = days. */
  async getAllBySensorId(
    sensorId: number,
    quadrante: number,
    numberOfReads: number = 7
  ): Promise<ReadEntry[]> {
    const res = await api.get<ReadEntry[]>('reads/GetAllBySensorId', {
      params: { sensorId, quadrante, numberOfReads },
    });
    return res.data;
  },
};
