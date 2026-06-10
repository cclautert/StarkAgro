import api from './api';
import {
  CreateManualReadRequest,
  CreateManualReadResponse,
  GetReadByPivotIdResponse,
  ReadEntry,
} from '../types/api';

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

  async createManualRead(data: CreateManualReadRequest): Promise<CreateManualReadResponse> {
    const res = await api.post<CreateManualReadResponse>('reads/Add', data);
    return {
      ...res.data,
      value: data.value,
    };
  },

  /** Latest server reading for conflict resolution (last-write-wins). */
  async getLatestBySensorId(sensorId: number, quadrante: number): Promise<ReadEntry | null> {
    const readings = await this.getAllBySensorId(sensorId, quadrante, 1);
    if (!readings.length) return null;
    return readings.reduce((latest, entry) =>
      new Date(entry.date).getTime() > new Date(latest.date).getTime() ? entry : latest
    );
  },
};
