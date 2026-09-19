-- Executed after the reference schema has been created inside this transaction.
SET LOCAL search_path = pg_catalog;

DO $baseline$
DECLARE
    item RECORD;
    actual_name TEXT;
    actual_constraint TEXT;
    expected_signature TEXT;
    reparsed_signature TEXT;
    matches INTEGER;
BEGIN
    -- Lock only the 15 application tables; no row data is changed.
    FOR item IN
        SELECT tablename FROM pg_tables
        WHERE schemaname = '_identity_kyc_reference'
        ORDER BY tablename
    LOOP
        EXECUTE format('LOCK TABLE public.%I IN ACCESS EXCLUSIVE MODE NOWAIT', item.tablename);
    END LOOP;

    IF EXISTS (
        WITH columns AS (
            SELECT n.nspname, c.relname, a.attname,
                   format_type(a.atttypid, a.atttypmod) AS data_type,
                   a.attnotnull, a.attidentity, a.attgenerated,
                   pg_get_expr(d.adbin, d.adrelid) AS default_value
            FROM pg_attribute a
            JOIN pg_class c ON c.oid = a.attrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            LEFT JOIN pg_attrdef d ON d.adrelid = c.oid AND d.adnum = a.attnum
            WHERE a.attnum > 0 AND NOT a.attisdropped AND c.relkind = 'r'
              AND n.nspname IN ('public', '_identity_kyc_reference')
              AND c.relname IN (
                  SELECT tablename FROM pg_tables
                  WHERE schemaname = '_identity_kyc_reference'
              )
        )
        SELECT 1
        FROM (SELECT * FROM columns WHERE nspname = '_identity_kyc_reference') expected
        FULL JOIN (SELECT * FROM columns WHERE nspname = 'public') actual
            USING (relname, attname)
        WHERE ROW(expected.data_type, expected.attnotnull, expected.attidentity,
                  expected.attgenerated, expected.default_value)
              IS DISTINCT FROM
              ROW(actual.data_type, actual.attnotnull, actual.attidentity,
                  actual.attgenerated, actual.default_value)
           OR expected.nspname IS NULL OR actual.nspname IS NULL
    ) THEN
        RAISE EXCEPTION 'Baseline stopped: column types, nullability, defaults or identity settings differ';
    END IF;

    -- Match primary keys, foreign keys and CHECK constraints by definition.
    -- Names from the handwritten SQL are normalized to the EF migration names.
    FOR item IN
        SELECT c.relname, k.conname, k.contype,
               replace(pg_get_constraintdef(k.oid), '_identity_kyc_reference.', 'public.') AS definition
        FROM pg_constraint k
        JOIN pg_class c ON c.oid = k.conrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = '_identity_kyc_reference' AND k.contype IN ('p', 'f', 'c')
        ORDER BY k.contype, c.relname, k.conname
    LOOP
        reparsed_signature := item.definition;
        IF item.contype = 'c' THEN
            -- PostgreSQL may canonicalize text-array casts after a dump/restore.
            -- Reparse the same CHECK on the scratch table to accept that form too.
            EXECUTE format('ALTER TABLE _identity_kyc_reference.%I DROP CONSTRAINT %I',
                           item.relname, item.conname);
            EXECUTE format('ALTER TABLE _identity_kyc_reference.%I ADD CONSTRAINT %I %s',
                           item.relname, item.conname, item.definition);
            SELECT pg_get_constraintdef(k.oid) INTO reparsed_signature
            FROM pg_constraint k
            WHERE k.conrelid = format('_identity_kyc_reference.%I', item.relname)::regclass
              AND k.conname = item.conname;
        END IF;
        SELECT count(*), min(k.conname::TEXT)
        INTO matches, actual_name
        FROM pg_constraint k
        WHERE k.conrelid = format('public.%I', item.relname)::regclass
          AND k.contype = item.contype
          AND pg_get_constraintdef(k.oid) IN (item.definition, reparsed_signature)
          AND k.convalidated;
        IF matches <> 1 THEN
            RAISE EXCEPTION 'Baseline stopped: constraint mismatch on %.%', item.relname, item.conname;
        END IF;
        IF actual_name <> item.conname THEN
            EXECUTE format('ALTER TABLE public.%I RENAME CONSTRAINT %I TO %I',
                           item.relname, actual_name, item.conname);
        END IF;
    END LOOP;

    IF (
        SELECT count(*) FROM pg_constraint k JOIN pg_class c ON c.oid = k.conrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public' AND k.contype IN ('p', 'f', 'c', 'x')
          AND c.relname IN (SELECT tablename FROM pg_tables WHERE schemaname = '_identity_kyc_reference')
    ) <> (
        SELECT count(*) FROM pg_constraint k JOIN pg_class c ON c.oid = k.conrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = '_identity_kyc_reference' AND k.contype IN ('p', 'f', 'c', 'x')
    ) THEN
        RAISE EXCEPTION 'Baseline stopped: unexpected constraints';
    END IF;

    -- EF models these uniqueness rules as indexes. Convert equivalent UNIQUE
    -- constraints into unique indexes while holding the table locks.
    -- No CASCADE: unexpected dependents cause the entire transaction to fail.
    FOR item IN
        SELECT c.relname, idx.relname AS index_name, i.indisunique,
               substring(pg_get_indexdef(i.indexrelid) FROM ' USING .*') AS signature,
               replace(pg_get_indexdef(i.indexrelid), '_identity_kyc_reference.', 'public.') AS definition
        FROM pg_index i
        JOIN pg_class c ON c.oid = i.indrelid
        JOIN pg_class idx ON idx.oid = i.indexrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = '_identity_kyc_reference' AND NOT i.indisprimary
    LOOP
        EXECUTE format('DROP INDEX _identity_kyc_reference.%I', item.index_name);
        EXECUTE replace(item.definition, 'public.', '_identity_kyc_reference.');
        SELECT substring(pg_get_indexdef(format('_identity_kyc_reference.%I', item.index_name)::regclass)
                         FROM ' USING .*') INTO reparsed_signature;
        SELECT count(*), min(idx.relname::TEXT)
        INTO matches, actual_name
        FROM pg_index i JOIN pg_class idx ON idx.oid = i.indexrelid
        WHERE i.indrelid = format('public.%I', item.relname)::regclass
          AND NOT i.indisprimary AND i.indisunique = item.indisunique
          AND i.indisvalid AND i.indisready
          AND substring(pg_get_indexdef(i.indexrelid) FROM ' USING .*')
              IN (item.signature, reparsed_signature);
        IF matches <> 1 THEN
            RAISE EXCEPTION 'Baseline stopped: index mismatch on %.%', item.relname, item.index_name;
        END IF;

        SELECT k.conname INTO actual_constraint
        FROM pg_constraint k
        WHERE k.conindid = format('public.%I', actual_name)::regclass AND k.contype = 'u';
        IF actual_constraint IS NOT NULL THEN
            EXECUTE format('ALTER TABLE public.%I DROP CONSTRAINT %I', item.relname, actual_constraint);
            EXECUTE item.definition;
        ELSIF actual_name <> item.index_name THEN
            EXECUTE format('ALTER INDEX public.%I RENAME TO %I', actual_name, item.index_name);
        END IF;
    END LOOP;

    IF (
        SELECT count(*) FROM pg_index i JOIN pg_class c ON c.oid = i.indrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relname IN (SELECT tablename FROM pg_tables WHERE schemaname = '_identity_kyc_reference')
    ) <> (
        SELECT count(*) FROM pg_index i JOIN pg_class c ON c.oid = i.indrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = '_identity_kyc_reference'
    ) THEN
        RAISE EXCEPTION 'Baseline stopped: unexpected indexes';
    END IF;

    FOR item IN
        SELECT replace(pg_get_triggerdef(t.oid), '_identity_kyc_reference.', 'public.') AS definition
        FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = '_identity_kyc_reference' AND NOT t.tgisinternal
    LOOP
        IF NOT EXISTS (
            SELECT 1 FROM pg_trigger t
            WHERE t.tgrelid = 'public."Users"'::regclass AND NOT t.tgisinternal
              AND t.tgenabled = 'O' AND pg_get_triggerdef(t.oid) = item.definition
        ) THEN
            RAISE EXCEPTION 'Baseline stopped: registration trigger mismatch';
        END IF;
    END LOOP;
    IF (SELECT count(*) FROM pg_trigger
        WHERE tgrelid = 'public."Users"'::regclass AND NOT tgisinternal) <> 3 THEN
        RAISE EXCEPTION 'Baseline stopped: unexpected user triggers';
    END IF;

    -- Install the reviewed migration function bodies without changing table data.
    FOR item IN
        SELECT replace(pg_get_functiondef(p.oid), '_identity_kyc_reference.', 'public.') AS definition
        FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
        WHERE n.nspname = '_identity_kyc_reference'
    LOOP
        EXECUTE item.definition;
    END LOOP;

    IF (SELECT count(*) FROM public."Roles"
        WHERE ("Name", "NormalizedName") IN
            (('User','USER'), ('KycReviewer','KYCREVIEWER'), ('Admin','ADMIN'))) <> 3 THEN
        RAISE EXCEPTION 'Baseline stopped: required roles are missing';
    END IF;
END;
$baseline$;
