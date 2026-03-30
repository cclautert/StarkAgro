import api from './api';
import { User, EditUserRequest } from '../types/api';

export const userService = {
  async getById(id: number): Promise<User> {
    const res = await api.get<User>('user/getById', { params: { id } });
    return res.data;
  },

  async update(data: EditUserRequest & { currentUserId: number }): Promise<User> {
    const res = await api.put<User>('user/update', data);
    return res.data;
  },
};
