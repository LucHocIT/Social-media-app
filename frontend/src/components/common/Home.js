import React from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../services/AuthContext';

const Home = () => {
  const { isAuthenticated } = useAuth();

  return (
    <div className="text-center">
      <h1 className="display-4 mb-4">Welcome to SocialApp</h1>
      
      <p className="lead mb-4">
        A place to connect with friends and share your thoughts.
      </p>
      
      {!isAuthenticated && (
        <div className="d-flex justify-content-center gap-3">
          <Link to="/register" className="btn btn-primary btn-lg">
            Sign Up
          </Link>
          <Link to="/login" className="btn btn-outline-primary btn-lg">
            Login
          </Link>
        </div>
      )}
      
      {isAuthenticated && (
        <div className="mt-4">
          <Link to="/profile" className="btn btn-primary btn-lg">
            Go to Profile
          </Link>
        </div>
      )}
      
      <div className="row mt-5">
        <div className="col-md-4">
          <div className="card mb-4">
            <div className="card-body">
              <h3 className="card-title">Connect</h3>
              <p className="card-text">
                Connect with friends, family, and interesting people around the world.
              </p>
            </div>
          </div>
        </div>
        
        <div className="col-md-4">
          <div className="card mb-4">
            <div className="card-body">
              <h3 className="card-title">Share</h3>
              <p className="card-text">
                Share your thoughts, experiences, and moments with your network.
              </p>
            </div>
          </div>
        </div>
        
        <div className="col-md-4">
          <div className="card mb-4">
            <div className="card-body">
              <h3 className="card-title">Discover</h3>
              <p className="card-text">
                Discover new content, ideas, and people based on your interests.
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Home;
