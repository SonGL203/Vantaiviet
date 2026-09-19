namespace VantaiViet.CoreApi.Data;

public static class ShipmentBrokerSql
{
    public const string Create = """
        CREATE FUNCTION public."CanDispatchShipment"(shipment_id uuid, actor_id uuid)
        RETURNS boolean LANGUAGE sql STABLE AS $$
            SELECT EXISTS (
                SELECT 1 FROM public."Shipments" s
                JOIN public."Users" u ON u."Id" = actor_id
                JOIN public."UserRoles" ur ON ur."UserId" = u."Id"
                JOIN public."Roles" r ON r."Id" = ur."RoleId"
                WHERE s."Id" = shipment_id AND u."AccountStatus" IN ('Active','PendingKyc')
                AND ((s."OwnerUserId" = actor_id AND
                    (r."NormalizedName" = 'BROKER' OR (NOT s."IsExternalOrder" AND r."NormalizedName" = 'SHIPPER')))
                OR (NOT s."IsExternalOrder" AND r."NormalizedName" = 'BROKER' AND EXISTS (
                    SELECT 1 FROM public."ShipmentBrokers" a WHERE a."ShipmentId" = shipment_id
                    AND a."BrokerUserId" = actor_id AND a."Status" = 'Accepted'))))
        $$;
        """;
}
