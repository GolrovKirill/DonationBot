CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    telegram_id BIGINT NOT NULL UNIQUE,
    username VARCHAR(100),
    first_name VARCHAR(100),
    last_name VARCHAR(100),
    admin BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS donation_goals (
    id SERIAL PRIMARY KEY,
    title VARCHAR(255) NOT NULL,
    description TEXT,
    target_amount DECIMAL(10, 2) NOT NULL,
    current_amount DECIMAL(10, 2) DEFAULT 0,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS donations (
    id SERIAL PRIMARY KEY,
    user_telegram_id BIGINT NOT NULL,
    goal_id INTEGER NOT NULL,
    amount DECIMAL(10, 2) NOT NULL,
    currency VARCHAR(3) DEFAULT 'RUB',
    provider_payment_id VARCHAR(255) NOT NULL,
    status VARCHAR(50) DEFAULT 'pending',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    
    -- Ограничения
    CONSTRAINT chk_donation_amount_positive CHECK (amount > 0),
    CONSTRAINT uq_provider_payment_id UNIQUE (provider_payment_id),
    
    -- Внешние ключи
    CONSTRAINT fk_donations_user_id 
        FOREIGN KEY (user_telegram_id) 
        REFERENCES users(telegram_id) ON DELETE RESTRICT,
        
    CONSTRAINT fk_donations_goal_id 
        FOREIGN KEY (goal_id) 
        REFERENCES donation_goals(id) ON DELETE RESTRICT
);

-- Индексы
CREATE INDEX IF NOT EXISTS idx_users_telegram_id ON users(telegram_id);
CREATE INDEX IF NOT EXISTS idx_donation_goals_active ON donation_goals(is_active) WHERE is_active = true;
CREATE INDEX IF NOT EXISTS idx_donations_user_telegram_id ON donations(user_telegram_id);
CREATE INDEX IF NOT EXISTS idx_donations_goal_id ON donations(goal_id);