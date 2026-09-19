namespace VantaiViet.CoreApi.Data;

public static class PasswordRecoverySql
{
    public const string Create = """
        CREATE FUNCTION public."CanQueueOtp"(user_id uuid, destination text, requested_at timestamptz, hourly_limit integer)
        RETURNS boolean LANGUAGE sql STABLE AS $$
            SELECT NOT EXISTS (SELECT 1 FROM public."OtpDeliveries" d
                WHERE (d."UserId" = user_id OR d."Destination" = destination) AND d."CreatedAt" > requested_at - interval '1 minute')
            AND (SELECT count(*) FROM public."OtpDeliveries" d
                WHERE (d."UserId" = user_id OR d."Destination" = destination) AND d."CreatedAt" > requested_at - interval '1 hour') < 5
            AND (SELECT count(*) FROM public."OtpDeliveries" d WHERE d."CreatedAt" > requested_at - interval '1 hour') < hourly_limit
        $$;
        """;
}
