-- Alternative: Test PostgreSQL TO_TIMESTAMP function for precise parsing
SELECT 
    'TO_TIMESTAMP approach' as test_type,
    '2025-08-01T20:43:45.413614Z' as input_string,
    TO_TIMESTAMP('2025-08-01T20:43:45.413614', 'YYYY-MM-DD"T"HH24:MI:SS.US') AT TIME ZONE 'UTC' as parsed_timestamp,
    EXTRACT(MICROSECONDS FROM TO_TIMESTAMP('2025-08-01T20:43:45.413614', 'YYYY-MM-DD"T"HH24:MI:SS.US') AT TIME ZONE 'UTC') as microseconds,
    TO_CHAR(TO_TIMESTAMP('2025-08-01T20:43:45.413614', 'YYYY-MM-DD"T"HH24:MI:SS.US') AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI:SS.US TZ') as formatted;

-- This explicitly tells PostgreSQL the exact format including microseconds