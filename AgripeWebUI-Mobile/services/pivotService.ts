import api from './api';
import { Pivot, CreatePivotRequest, EditPivotRequest } from '../types/api';

export const pivotService = {
  async getAll(): Promise<Pivot[]> {
    const res = await api.get<Pivot[]>('pivot/getAll');
    return res.data;
  },

  async getById(id: number): Promise<Pivot> {
    const res = await api.get<Pivot>('pivot/getById', { params: { id } });
    return res.data;
  },

  async add(data: CreatePivotRequest): Promise<{ id: number }> {
    const res = await api.post<{ id: number }>('pivot/add', data);
    return res.data;
  },

  async update(data: EditPivotRequest): Promise<{ id: number }> {
    const res = await api.put<{ id: number }>('pivot/update', data);
    return res.data;
  },

  async delete(id: number): Promise<void> {
    await api.delete('pivot/delete', { params: { id } });
  },
};
