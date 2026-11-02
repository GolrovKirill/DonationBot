# 🤖 Donation Bot - Telegram Bot for Collecting Donations

[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-17-336791?logo=postgresql)](https://www.postgresql.org/)
[![Docker](https://img.shields.io/badge/Docker-✓-2496ED?logo=docker)](https://www.docker.com/)
[![Telegram](https://img.shields.io/badge/Telegram-API-26A5E4?logo=telegram)](https://core.telegram.org/bots/api)
[![Build](https://github.com/GolrovKirill/DonationBot/actions/workflows/blank.yml/badge.svg)](https://github.com/GolrovKirill/DonationBot/actions/workflows/blank.yml)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

> Телеграм-бот для автоматизации сбора пожертвований с поддержкой целей, статистики и админ-панели

---

## ✨ Особенности

### 💰 Основной функционал
- **Сбор пожертвований** через встроенные платежи Telegram Bot Payments
- **Установка целей** с отслеживанием прогресса в реальном времени
- **Реализация прав доступа** администраторы могут создавать цели для пожертвование, а пользователи жертвовать на них

### 📊 Аналитика и управление
- **Визуальная статистика** с графиками прогресса
- **Административная панель** для управления целями

## 🧱 Стек
| Слой | Технологии |
|---|---|
| Платформа | .NET 9.0, C# |
| Telegram | Telegram.Bot (long polling) |
| База данных | PostgreSQL 17, Dapper |
| Архитектура | слоистая, паттерн Repository |
| Контейнеризация | Docker, Docker Compose |
| Тесты | NUnit |
| CI | GitHub Actions |
---
## 📋 Требования
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL 17](https://www.postgresql.org/download/) — для локального запуска без Docker
- [Docker](https://docs.docker.com/get-docker/) и Docker Compose — для запуска в контейнерах
Для работы бота понадобятся два токена:
1. **Токен бота** — получить у [@BotFather](https://t.me/BotFather)
2. **Токен платёжного провайдера** — в BotFather включить Payments
   (`/mybots` → бот → Payments) и подключить провайдера, например ЮKassa
---
## ⚙️ Конфигурация
Все настройки бота лежат в двух классах:
| Класс | Ключ | Что это |
|---|---|---|
| `BotConfig` | `BotToken` | токен бота от BotFather |
| `BotConfig` | `PaymentProviderToken` | токен платёжного провайдера |
| `DatabaseConfig` | `ConnectionString` | строка подключения к PostgreSQL |
---
## 🐳 Запуск через Docker Compose
Самый простой способ: одной командой поднимаются и база, и бот.
**1. Создай в корне проекта файл `.env`:**
```env
POSTGRES_USER=твои_данные
POSTGRES_PASSWORD=твои_данные
POSTGRES_DB=твои_данные
BotConfig__BotToken=123456789:AA...твой_токен
BotConfig__PaymentProviderToken=123456789:AA...твой_токен
DatabaseConfig__ConnectionString=Host=postgres;Port=5432;Database=donationbot;Username=donation;Password=donation
```
В строке подключения хост — **`postgres`** (имя сервиса из `docker-compose.yml`), а не `localhost`.
**2. Запусти:**
```bash
docker compose up --build
```
При первом запуске PostgreSQL автоматически применит схему из
`Data/Scripts/create_tables.sql` — таблицы создадутся сами.
**3. Остановить:**
```bash
docker compose down          # остановить
docker compose down -v       # остановить и удалить данные БД
```
---
## 💻 Локальный запуск без Docker
**1. Подготовь базу.** Создай БД и примени схему:
```bash
createdb donationbot
psql -d donationbot -f Data/Scripts/create_tables.sql
```
**2. Заполни `Bot/appsettings.json`:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "BotConfig": {
    "BotToken": "123456789:AA...твой_токен",
    "PaymentProviderToken": "123456789:AA...твой_токен"
  },
  "DatabaseConfig": {
    "ConnectionString": "Host=localhost;Port=5432;Database=donationbot;Username=postgres;Password=postgres"
  }
}
```
**3. Собери и запусти:**
```bash
dotnet restore
dotnet run --project Bot
```
---
## 🧪 Тесты
```bash
dotnet test
```
Тесты лежат в проекте `TestBot`.
---
## 📁 Структура проекта
```
DonationBot/
├── Bot/                    — точка входа, обработчики обновлений, polling
│   ├── Hadlers/            — обработчики команд и сценариев
│   ├── Services/           — сервисы приложения
│   ├── Program.cs          — конфигурация DI и запуск
│   └── appsettings.json    — настройки (токены, строка подключения)
├── Configurations/         — классы конфигурации (BotConfig, DatabaseConfig)
├── Data/                   — доступ к данным, SQL-скрипты схемы
│   └── Scripts/create_tables.sql
├── Services/               — бизнес-логика
├── TestBot/                — тесты
├── Dockerfile
├── docker-compose.yml
└── DonationBot.sln
```
---
