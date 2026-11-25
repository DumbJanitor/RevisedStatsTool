IF DB_ID('SaleScope') IS NULL
    CREATE DATABASE SaleScope;
GO

USE SaleScope;
GO

IF OBJECT_ID('[User]', 'U') IS NULL
BEGIN
    CREATE TABLE [User] (
        U_ID VARCHAR(255) PRIMARY KEY,
        U_Email VARCHAR(255) UNIQUE,
        U_Password VARCHAR(255),
        U_Name VARCHAR(255),
        U_Role VARCHAR(30)
    );
END;
GO

IF OBJECT_ID('Inventory', 'U') IS NULL
BEGIN
    CREATE TABLE Inventory (
        I_ID VARCHAR(255) PRIMARY KEY,
        U_ID VARCHAR(255),
        I_ProductName VARCHAR(255),
        I_Category VARCHAR(255),
        I_Unit VARCHAR(50),
        I_DateAdded DATE,
        CONSTRAINT FK_Inventory_User
            FOREIGN KEY (U_ID) REFERENCES [User](U_ID)
    );
END;
GO

IF OBJECT_ID('Sales', 'U') IS NULL
BEGIN
    CREATE TABLE Sales (
        S_ID VARCHAR(255) PRIMARY KEY,
        I_ID VARCHAR(255),
        S_Date DATE,
        S_Quantity INT,
        S_DailyStock INT,
        CONSTRAINT FK_Sales_Inventory
            FOREIGN KEY (I_ID) REFERENCES Inventory(I_ID)
    );
END;
GO
