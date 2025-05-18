import React, { createContext, useState, useContext, useEffect } from 'react';
import { authApi } from './api';

const AuthContext = createContext();

export function useAuth() {
  return useContext(AuthContext);
}

export function AuthProvider({ children }) {
  const [currentUser, setCurrentUser] = useState(null);
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    // Kiểm tra xem có token hay không
    const token = localStorage.getItem('token');
    if (token) {
      fetchCurrentUser();
    } else {
      setLoading(false);
    }
  }, []);

  // Lấy thông tin người dùng hiện tại
  const fetchCurrentUser = async () => {
    try {
      const response = await authApi.getCurrentUser();
      setCurrentUser(response.data);
      setIsAuthenticated(true);
    } catch (error) {
      console.error('Error fetching current user:', error);
      // Nếu có lỗi, xóa token
      localStorage.removeItem('token');
    } finally {
      setLoading(false);
    }
  };

  // Xử lý đăng nhập
  const login = async (username, password) => {
    try {
      const response = await authApi.login({ username, password });
      const { token, user } = response.data;
      
      // Lưu token vào localStorage
      localStorage.setItem('token', token);
      
      // Cập nhật state
      setCurrentUser(user);
      setIsAuthenticated(true);
      return user;
    } catch (error) {
      throw error;
    }
  };
  // Xử lý đăng ký
  const register = async (userData) => {
    try {
      console.log("AuthContext: Registering user with data:", userData);
      const response = await authApi.register(userData);
      console.log("AuthContext: Registration successful:", response.data);
      return response.data;
    } catch (error) {
      console.error("AuthContext: Registration failed:", error);
      
      // Provide more detailed error information
      if (error.response) {
        console.error("Error status:", error.response.status);
        console.error("Error data:", error.response.data);
      }
      
      throw error;
    }
  };

  // Xử lý đăng xuất
  const logout = async () => {
    try {
      await authApi.logout();
    } catch (error) {
      console.error('Error during logout:', error);
    } finally {
      // Luôn xóa token và cập nhật state
      localStorage.removeItem('token');
      setCurrentUser(null);
      setIsAuthenticated(false);
    }
  };

  const value = {
    currentUser,
    isAuthenticated,
    loading,
    login,
    register,
    logout
  };

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
}
