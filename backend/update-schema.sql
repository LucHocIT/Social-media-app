-- Add Role column with default value 'User'
ALTER TABLE Users
ADD Role nvarchar(20) NOT NULL DEFAULT 'User';
GO

-- Add IsDeleted column with default value 0 (false)
ALTER TABLE Users
ADD IsDeleted bit NOT NULL DEFAULT 0;
GO

-- Add DeletedAt column
ALTER TABLE Users
ADD DeletedAt datetime2 NULL;
GO

-- Create a stored procedure to create an admin user if it doesn't exist
CREATE PROCEDURE [dbo].[CreateAdminUserIfNotExists]
AS
BEGIN
    -- Check if admin user exists
    IF NOT EXISTS (SELECT * FROM Users WHERE Username = 'admin')
    BEGIN
        -- Insert admin user with hashed password 'Admin@123'
        INSERT INTO Users (Username, Email, PasswordHash, FirstName, LastName, Role, IsDeleted, CreatedAt, LastActive)
        VALUES ('admin', 'admin@example.com', '$2a$11$g9JX1rYsoFBTkBBY9NHA8eJvIvuR5c2vNzjz36/u2wntqT1OC.bOK', 'Admin', 'User', 'Admin', 0, GETUTCDATE(), GETUTCDATE());
        
        PRINT 'Admin user created successfully';
    END
    ELSE
    BEGIN
        -- Update existing admin user's role to Admin if it's not already
        UPDATE Users
        SET Role = 'Admin'
        WHERE Username = 'admin' AND Role != 'Admin';
        
        PRINT 'Admin user already exists, role updated if necessary';
    END
END;
GO

-- Execute the admin user creation procedure
EXEC [dbo].[CreateAdminUserIfNotExists];
GO
