CREATE DATABASE IF NOT EXISTS esd_system CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE esd_system;

CREATE TABLE IF NOT EXISTS esd_events (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    event_time DATETIME(3) NOT NULL,
    device_name VARCHAR(100) NOT NULL,
    employee_id VARCHAR(100) NULL,
    event_type VARCHAR(50) NOT NULL,
    status VARCHAR(30) NOT NULL,
    raw_data TEXT NULL,
    message TEXT NULL,
    INDEX ix_event_time (event_time),
    INDEX ix_employee_id (employee_id),
    INDEX ix_device_event (device_name, event_type)
);
