-- Direct test van het probleem: waarom worden microseconden fout opgeslagen?
-- Test met exacte waarde uit API

-- Test 1: Direct INSERT van API waarde
SELECT 
    'Direct API string' as test_type,
    '2025-08-01T20:43:45.413614Z' as input_value,
    '2025-08-01T20:43:45.413614Z'::timestamp(6) with time zone as result,
    EXTRACT(MICROSECONDS FROM '2025-08-01T20:43:45.413614Z'::timestamp(6) with time zone) as microseconds_extracted;

-- Test 2: NOW() functie (die wel werkt)
SELECT 
    'NOW() function' as test_type,
    NOW()::text as input_value,
    NOW() as result,
    EXTRACT(MICROSECONDS FROM NOW()) as microseconds_extracted;

-- Test 3: Manual constructie
SELECT 
    'Manual construction' as test_type,
    '2025-08-01 20:43:45.413614+00' as input_value,
    '2025-08-01 20:43:45.413614+00'::timestamp(6) with time zone as result,
    EXTRACT(MICROSECONDS FROM '2025-08-01 20:43:45.413614+00'::timestamp(6) with time zone) as microseconds_extracted;

-- Test 4: Check wat EXTRACT(MICROSECONDS) eigenlijk doet
SELECT 
    'Understanding EXTRACT' as test_type,
    '2025-08-01T20:43:45.413614Z' as input_value,
    EXTRACT(EPOCH FROM '2025-08-01T20:43:45.413614Z'::timestamp(6) with time zone) as epoch_seconds,
    EXTRACT(MICROSECONDS FROM '2025-08-01T20:43:45.413614Z'::timestamp(6) with time zone) as microseconds_field,
    -- MICROSECONDS field includes the seconds part! It's not just fractional seconds
    EXTRACT(MICROSECONDS FROM '2025-08-01T20:43:45.413614Z'::timestamp(6) with time zone) % 1000000 as true_microseconds;