import React, { useState, useEffect } from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import Navbar from './components/common/Navbar';
import Home from './components/common/Home';
import Login from './components/auth/Login';
import Register from './components/auth/Register';
import Profile from './components/user/Profile';
import UserDetails from './components/user/UserDetails';
import { AuthProvider, useAuth } from './services/AuthContext';

// PrivateRoute component to protect routes that require authentication
const PrivateRoute = ({ element }) => {
  const { isAuthenticated, loading } = useAuth();
  
  if (loading) {
    return <div>Loading...</div>;
  }
  
  return isAuthenticated ? element : <Navigate to="/login" />;
};

function App() {
  return (
    <AuthProvider>
      <div className="app">
        <Navbar />
        <main className="container mt-4">
          <Routes>
            <Route path="/" element={<Home />} />
            <Route path="/login" element={<Login />} />
            <Route path="/register" element={<Register />} />
            <Route 
              path="/profile" 
              element={<PrivateRoute element={<Profile />} />} 
            />
            <Route path="/users/:id" element={<UserDetails />} />
          </Routes>
        </main>
      </div>
    </AuthProvider>
  );
}

export default App;
