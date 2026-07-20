import api from './api';
import { Sensor, CreateSensorRequest, EditSensorRequest } from '../types/api';

export const sensorService = {
  async getAll(): Promise<Sensor[]> {
    const res = await api.get<Sensor[]>('sensor/getAll');
    return res.data;
  },

  async getById(id: number): Promise<Sensor> {
    const res = await api.get<Sensor>('sensor/getById', { params: { id } });
    return res.data;
  },

  async getAllByPivotId(pivotId: number, quadrante: number): Promise<Sensor[]> {
    const res = await api.get<Sensor[]>('sensor/getAllByPivotId', {
      params: { pivotId, quadrante },
    });
    return res.data;
  },

  async add(data: CreateSensorRequest): Promise<{ id: number }> {
    const res = await api.post<{ id: number }>('sensor/add', data);
    return res.data;
  },

  async update(data: EditSensorRequest): Promise<{ id: number }> {
    const res = await api.put<{ id: number }>('sensor/update', data);
    return res.data;
  },

  async delete(id: number): Promise<void> {
    await api.delete('sensor/delete', { params: { id } });
  },
};
