namespace VantaiViet.CoreApi.Data;

public static class TripRouteSql
{
    public const string Create = """
        CREATE FUNCTION public."ClaimTripRoute"(trip_id uuid, actor_id uuid, input_hash text, lease_id uuid, requested_at timestamptz)
        RETURNS integer LANGUAGE plpgsql AS $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1 FROM public."Trips" t JOIN public."Bookings" b ON b."Id" = t."BookingId"
                JOIN public."Users" u ON u."Id" = actor_id
                WHERE t."Id" = trip_id AND u."AccountStatus" IN ('Active', 'PendingKyc')
                AND (b."OwnerUserId" = actor_id OR t."DriverUserId" = actor_id OR EXISTS (
                    SELECT 1 FROM public."TripParticipants" p WHERE p."TripId" = trip_id AND p."UserId" = actor_id)))
            THEN RETURN 0; END IF;
            INSERT INTO public."TripRoutes" ("TripId", "InputHash", "LeaseId", "LeaseUntil", "ExpiresAt")
            VALUES (trip_id, input_hash, lease_id, requested_at + interval '15 seconds', requested_at)
            ON CONFLICT ("TripId", "InputHash") DO UPDATE
            SET "LeaseId" = EXCLUDED."LeaseId", "LeaseUntil" = EXCLUDED."LeaseUntil"
            WHERE "TripRoutes"."ExpiresAt" <= requested_at AND "TripRoutes"."LeaseUntil" <= requested_at;
            IF FOUND THEN RETURN 1; END IF;
            IF EXISTS (SELECT 1 FROM public."TripRoutes" WHERE "TripId" = trip_id AND "InputHash" = input_hash
                AND "ExpiresAt" > requested_at AND "ResponseJson" IS NOT NULL)
            THEN RETURN 3; END IF;
            RETURN 2;
        END $$;
        """;
}
