CREATE TABLE tickets (
    id SERIAL PRIMARY KEY,
    title VARCHAR(120) NOT NULL,
    description TEXT NOT NULL,
    priority INTEGER NOT NULL CHECK (priority BETWEEN 1 AND 3),
    status VARCHAR(20) NOT NULL DEFAULT 'new' CHECK (status IN ('new', 'in_progress', 'done')),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

INSERT INTO tickets (title, description, priority, status, created_at) VALUES
    ('Замена картриджа', 'Закончился тонер в принтере на третьем этаже.', 1, 'done', now() - interval '7 days'),
    ('Настроить VPN', 'Нужен доступ к рабочей сети с домашнего компьютера.', 2, 'in_progress', now() - interval '6 days'),
    ('Сбой в 1С', 'При проведении документа появляется ошибка.', 3, 'new', now() - interval '5 days'),
    ('Не открывается Excel', 'Файл отчёта не загружается, пишет про повреждение.', 2, 'in_progress', now() - interval '4 days'),
    ('Пропал интернет в отделе', 'В кабинете 118 нет сети на всех компьютерах.', 3, 'done', now() - interval '3 days'),
    ('Заявка на новый монитор', 'Старый монитор мерцает и мешает работе.', 1, 'new', now() - interval '2 days'),
    ('Ошибка входа в почту', 'Пароль не принимается после смены.', 2, 'new', now() - interval '1 day');
