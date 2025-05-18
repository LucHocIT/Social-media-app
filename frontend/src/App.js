import React, { useState, useEffect } from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import Navbar from './components/common/Navbar';
import Home from './components/common/Home';
import Profile from './components/user/Profile';
import UserDetails from './components/user/UserDetails';
import { AuthProvider, useAuth } from './services/AuthContext';

// PrivateRoute component to protect routes that require authentication
const PrivateRoute = ({ element }) => {
  const { isAuthenticated, loading } = useAuth();
  
  if (loading) {
    return (
      <div className="d-flex justify-content-center align-items-center" style={{ height: "400px" }}>
        <div className="spinner-border text-primary" role="status">
          <span className="visually-hidden">Loading...</span>
        </div>
      </div>
    );
  }
  
  return isAuthenticated ? element : <Navigate to="/" />;
};

function App() {
  return (
    <AuthProvider>
      <div className="app">
        <Navbar />
        <main className="container-fluid p-0">
          <Routes>
            <Route path="/" element={<Home />} />
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
