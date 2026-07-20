import api from './api';
import { LoginRequest, LoginResponse, ExternalLoginRequest, CreateUserRequest } from '../types/api';

export const authService = {
  async login(data: LoginRequest): Promise<LoginResponse> {
    const res = await api.post<LoginResponse>('Auth/LogIn', data);
    return res.data;
  },

  async externalLogin(data: ExternalLoginRequest): Promise<LoginResponse | null> {
    const res = await api.post<LoginResponse>('Auth/external-login', data);
    return res.data;
  },

  async register(data: CreateUserRequest): Promise<void> {
    await api.post('Auth/addUser', data);
  },
};
