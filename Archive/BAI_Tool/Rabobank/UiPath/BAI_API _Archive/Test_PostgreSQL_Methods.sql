-- Backup solution: Test different PostgreSQL timestamp parsing methods

-- Test 1: Direct casting with parentheses
SELECT '('||'2025-08-01T20:43:45.413614Z'||')::timestamp(6) with time zone' as method,
       ('2025-08-01T20:43:45.413614Z')::timestamp(6) with time zone as result,
       EXTRACT(MICROSECONDS FROM ('2025-08-01T20:43:45.413614Z')::timestamp(6) with time zone) as microseconds;

-- Test 2: Using timezone conversion function
SELECT 'timezone conversion' as method,
       timezone('UTC', '2025-08-01T20:43:45.413614'::timestamp(6)) as result,
       EXTRACT(MICROSECONDS FROM timezone('UTC', '2025-08-01T20:43:45.413614'::timestamp(6))) as microseconds;

-- Test 3: Simple string with explicit type
SELECT 'simple cast' as method,
       '2025-08-01T20:43:45.413614+00'::timestamp(6) with time zone as result,
       EXTRACT(MICROSECONDS FROM '2025-08-01T20:43:45.413614+00'::timestamp(6) with time zone) as microseconds;