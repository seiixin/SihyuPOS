USE sihyu_pos;

-- 1. Seed work schedules
INSERT IGNORE INTO work_schedule (label, days_mask, is_active, updated_at)
VALUES 
('Mon–Fri', 0b1111100, 1, NOW()),
('MWF', 0b1010100, 1, NOW()),
('TTh', 0b0101000, 1, NOW()),
('Daily', 0b1111111, 1, NOW());

-- 2. Seed position salaries
INSERT IGNORE INTO position_salary (position, daily_rate, is_active, updated_at)
VALUES 
('Manager', 800.00, 1, NOW()),
('Cashier', 450.00, 1, NOW()),
('Waiter', 400.00, 1, NOW()),
('Cook', 500.00, 1, NOW()),
('Dishwasher', 350.00, 1, NOW());

-- 3. Seed tables
INSERT IGNORE INTO cafe_tables (table_number, is_available)
VALUES 
('1', 1),
('2', 1),
('3', 1),
('4', 1),
('5', 1);

-- 4. Seed admin user (password is "admin123" hashed with BCrypt)
INSERT INTO employees (full_name, position, created_at) 
VALUES ('System Administrator', 'Manager', NOW());

SET @admin_employee_id = LAST_INSERT_ID();

INSERT INTO users (email, password, role, employee_id, is_active, created_at)
VALUES ('admin@sihyupos.com', '$2a$11$kZb/a1Ql6z3k0LdQ5kYqweQ1uY0Q3QrN5c7Wp4aTzE6X6V6dW5aW6', 'Admin', @admin_employee_id, 1, NOW());

-- 5. Seed employee user (password is "employee123")
INSERT INTO employees (full_name, position, created_at) 
VALUES ('Juan Dela Cruz', 'Waiter', NOW());

SET @employee_employee_id = LAST_INSERT_ID();

INSERT INTO users (email, password, role, employee_id, is_active, created_at)
VALUES ('employee@sihyupos.com', '$2a$11$6TQ9q1hW1w5x6r5Lq2U8CubL.9g6w3F4zVr2cE9Kq7w7N9t7a8', 'Employee', @employee_employee_id, 1, NOW());

-- 6. Seed cashier user (password is "cashier123")
INSERT INTO employees (full_name, position, created_at) 
VALUES ('Maria Santos', 'Cashier', NOW());

SET @cashier_employee_id = LAST_INSERT_ID();

INSERT INTO users (email, password, role, employee_id, is_active, created_at)
VALUES ('cashier@sihyupos.com', '$2a$11$8Qq5b9HdK3gT1aV5dF9G8ubR.3d7w2Z1cX4vB6nK1m5n8Qq2c', 'Cashier', @cashier_employee_id, 1, NOW());
