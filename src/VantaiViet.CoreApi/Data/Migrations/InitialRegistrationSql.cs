namespace VantaiViet.CoreApi.Data.Migrations;

// Frozen SQL for the initial migration; future changes belong in a new migration.
internal static class InitialRegistrationSql
{
    internal const string Up = """
        CREATE FUNCTION public."ValidateUserRegistration"()
        RETURNS TRIGGER LANGUAGE plpgsql AS $$
        DECLARE
            registration_row public."RegistrationApplications"%ROWTYPE;
            approved_kyc_id UUID;
            document_count INTEGER;
        BEGIN
            SELECT * INTO registration_row
            FROM public."RegistrationApplications"
            WHERE "Id" = NEW."RegistrationApplicationId" FOR UPDATE;
            IF NOT FOUND THEN
                RAISE EXCEPTION 'Registration application does not exist';
            END IF;
            IF registration_row."Status" <> 'InProgress' THEN
                RAISE EXCEPTION 'Registration application is not active';
            END IF;
            IF registration_row."ExpiresAt" <= CURRENT_TIMESTAMP THEN
                RAISE EXCEPTION 'Registration application has expired';
            END IF;
            IF registration_row."PhoneVerifiedAt" IS NULL THEN
                RAISE EXCEPTION 'Phone number has not been verified';
            END IF;
            IF NEW."PhoneNumber" <> registration_row."PhoneNumber"
               OR NOT NEW."PhoneNumberConfirmed" THEN
                RAISE EXCEPTION 'User phone does not match verified registration';
            END IF;
            SELECT "Id" INTO approved_kyc_id
            FROM public."KycApplications"
            WHERE "RegistrationApplicationId" = registration_row."Id"
              AND "Status" = 'Approved' FOR UPDATE;
            IF approved_kyc_id IS NULL THEN
                RAISE EXCEPTION 'Approved KYC is required';
            END IF;
            PERFORM 1 FROM public."KycIdentityDetails"
            WHERE "ApplicationId" = approved_kyc_id;
            IF NOT FOUND THEN
                RAISE EXCEPTION 'KYC identity details are missing';
            END IF;
            SELECT COUNT(*) INTO document_count FROM public."KycDocuments"
            WHERE "ApplicationId" = approved_kyc_id AND "DeletedAt" IS NULL;
            IF document_count <> 3 THEN
                RAISE EXCEPTION 'CCCD front, CCCD back and selfie are required';
            END IF;
            RETURN NEW;
        END;
        $$;
        CREATE TRIGGER "TR_Users_ValidateRegistration"
        BEFORE INSERT ON public."Users"
        FOR EACH ROW EXECUTE FUNCTION public."ValidateUserRegistration"();
        
        CREATE FUNCTION public."CompleteUserRegistration"()
        RETURNS TRIGGER LANGUAGE plpgsql AS $$
        BEGIN
            UPDATE public."RegistrationApplications"
            SET "Status" = 'Completed',
                "CompletedAt" = CURRENT_TIMESTAMP,
                "UpdatedAt" = CURRENT_TIMESTAMP
            WHERE "Id" = NEW."RegistrationApplicationId";
            RETURN NEW;
        END;
        $$;
        CREATE TRIGGER "TR_Users_CompleteRegistration"
        AFTER INSERT ON public."Users"
        FOR EACH ROW EXECUTE FUNCTION public."CompleteUserRegistration"();
        
        CREATE FUNCTION public."ProtectUserRegistrationLink"()
        RETURNS TRIGGER LANGUAGE plpgsql AS $$
        BEGIN
            IF NEW."RegistrationApplicationId"
               IS DISTINCT FROM OLD."RegistrationApplicationId" THEN
                RAISE EXCEPTION 'User registration link cannot be changed';
            END IF;
            RETURN NEW;
        END;
        $$;
        CREATE TRIGGER "TR_Users_ProtectRegistrationLink"
        BEFORE UPDATE OF "RegistrationApplicationId" ON public."Users"
        FOR EACH ROW EXECUTE FUNCTION public."ProtectUserRegistrationLink"();
        
        INSERT INTO public."Roles" ("Name", "NormalizedName", "ConcurrencyStamp")
        VALUES ('User', 'USER', gen_random_uuid()::TEXT),
               ('KycReviewer', 'KYCREVIEWER', gen_random_uuid()::TEXT),
               ('Admin', 'ADMIN', gen_random_uuid()::TEXT)
        ON CONFLICT ("NormalizedName") DO NOTHING;
        """;

    internal const string Down = """
        DROP TRIGGER "TR_Users_ValidateRegistration" ON public."Users";
        DROP TRIGGER "TR_Users_CompleteRegistration" ON public."Users";
        DROP TRIGGER "TR_Users_ProtectRegistrationLink" ON public."Users";
        DROP FUNCTION public."ValidateUserRegistration"();
        DROP FUNCTION public."CompleteUserRegistration"();
        DROP FUNCTION public."ProtectUserRegistrationLink"();
        """;
}
