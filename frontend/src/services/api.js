import axios from 'axios';

// Tạo instance axios với URL cơ sở
const api = axios.create({
  baseURL: 'http://localhost:5062/api'
});

// Thêm interceptor cho yêu cầu để tự động thêm token vào header
api.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('token');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

export const authApi = {
  register: (userData) => api.post('/auth/register', userData),
  login: (credentials) => api.post('/auth/login', credentials),
  logout: () => api.post('/auth/logout'),
  getCurrentUser: () => api.get('/users/me'),
  verifyEmail: (email) => api.post('/auth/verifyemail', { email })
};

export const userApi = {
  getUserById: (id) => api.get(`/users/${id}`)
};

export default api;
