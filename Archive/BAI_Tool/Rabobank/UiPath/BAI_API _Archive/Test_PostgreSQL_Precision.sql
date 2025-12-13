-- Test exact precision handling in PostgreSQL
-- Run this to verify how PostgreSQL handles the timestamp strings

-- Test direct casting methods
SELECT 
    'Original API value' as test_type,
    '2025-08-01T20:43:45.413614Z' as input_string,
    '2025-08-01T20:43:45.413614Z'::timestamp with time zone as parsed_timestamp,
    EXTRACT(MICROSECONDS FROM '2025-08-01T20:43:45.413614Z'::timestamp with time zone) as microseconds,
    TO_CHAR('2025-08-01T20:43:45.413614Z'::timestamp with time zone, 'YYYY-MM-DD HH24:MI:SS.US TZ') as formatted

UNION ALL

SELECT 
    'With explicit precision',
    '2025-08-01T20:43:45.413614Z',
    '2025-08-01T20:43:45.413614Z'::timestamp(6) with time zone,
    EXTRACT(MICROSECONDS FROM '2025-08-01T20:43:45.413614Z'::timestamp(6) with time zone),
    TO_CHAR('2025-08-01T20:43:45.413614Z'::timestamp(6) with time zone, 'YYYY-MM-DD HH24:MI:SS.US TZ')

UNION ALL

SELECT 
    'Using TIMESTAMPTZ function',
    '2025-08-01T20:43:45.413614Z',
    TIMESTAMPTZ '2025-08-01T20:43:45.413614Z',
    EXTRACT(MICROSECONDS FROM TIMESTAMPTZ '2025-08-01T20:43:45.413614Z'),
    TO_CHAR(TIMESTAMPTZ '2025-08-01T20:43:45.413614Z', 'YYYY-MM-DD HH24:MI:SS.US TZ')

UNION ALL

SELECT 
    'Database stored value',
    '2025-08-01 20:43:45+00',
    '2025-08-01 20:43:45+00'::timestamp with time zone,
    EXTRACT(MICROSECONDS FROM '2025-08-01 20:43:45+00'::timestamp with time zone),
    TO_CHAR('2025-08-01 20:43:45+00'::timestamp with time zone, 'YYYY-MM-DD HH24:MI:SS.US TZ');

-- Expected result: TIMESTAMPTZ should preserve the .413614 microseconds correctly