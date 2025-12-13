ALTER TABLE rpa_data.bai_monitor_users RENAME TO cashapp_users;

SELECT tablename, schemaname FROM pg_tables WHERE schemaname = 'rpa_data' AND tablename = 'cashapp_users';

SELECT column_name, data_type FROM information_schema.columns WHERE table_schema = 'rpa_data' AND table_name = 'cashapp_users' ORDER BY ordinal_position;
