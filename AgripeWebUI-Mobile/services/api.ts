import axios from 'axios';
import { router } from 'expo-router';
import { API_BASE_URL } from '../constants/api';
import { tokenStorage } from './tokenStorage';

const api = axios.create({
  baseURL: API_BASE_URL,
  timeout: 15000,
});

api.interceptors.request.use(async (config) => {
  const token = await tokenStorage.getToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    if (error.response?.status === 401) {
      await tokenStorage.removeToken();
      router.replace('/(auth)/login');
    }
    return Promise.reject(error);
  }
);

export default api;
