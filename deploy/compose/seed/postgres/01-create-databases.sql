-- Database per service. The identity database is created through POSTGRES_DB.
CREATE DATABASE tenants_users;
-- Each service's role is granted its own database; nothing else can connect by default.
REVOKE CONNECT ON DATABASE tenants_users FROM PUBLIC;
