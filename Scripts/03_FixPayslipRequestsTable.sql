USE sihyu_pos;

-- Rename the table from payslip_requests to PayslipRequests (PascalCase)
RENAME TABLE payslip_requests TO PayslipRequests;

-- Rename columns to PascalCase to match the C# code
ALTER TABLE PayslipRequests 
CHANGE COLUMN id Id INT AUTO_INCREMENT,
CHANGE COLUMN employee_id EmployeeId INT NOT NULL,
CHANGE COLUMN payroll_id PayrollId INT NULL,
CHANGE COLUMN full_name FullName VARCHAR(255) NULL,
CHANGE COLUMN request_date RequestDate DATETIME DEFAULT CURRENT_TIMESTAMP,
CHANGE COLUMN updated_date UpdatedDate DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP;

-- Also check the Payslips table for the same issue
RENAME TABLE payslips TO Payslips;
ALTER TABLE Payslips
CHANGE COLUMN payslip_id PayslipId INT AUTO_INCREMENT,
CHANGE COLUMN employee_id EmployeeId INT NOT NULL,
CHANGE COLUMN pay_date PayDate DATE NOT NULL,
CHANGE COLUMN hours_worked HoursWorked DECIMAL(10, 2) NOT NULL DEFAULT 0,
CHANGE COLUMN rate_per_hour RatePerHour DECIMAL(10, 2) NOT NULL DEFAULT 0,
CHANGE COLUMN net_salary NetSalary DECIMAL(12, 2) NOT NULL DEFAULT 0,
CHANGE COLUMN updated_date UpdatedDate DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP;
