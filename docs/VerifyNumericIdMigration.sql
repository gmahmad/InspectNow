-- Verification harness, not the deployment script.
-- Run the whole file in pgAdmin Query Tool using a local development database.
-- Uses synthetic data in an isolated schema and ROLLS BACK both scenarios.
-- It never reads or changes your public application tables.
-- If an error stops execution, run ROLLBACK; in the same Query Tool session.
-- A pre-existing inspectnow_migration_check schema causes a safe failure.
-- The migration body below is the shipped body with ONLY its schema qualified
-- names redirected to inspectnow_migration_check.

BEGIN;
CREATE SCHEMA inspectnow_migration_check;
SET LOCAL search_path TO inspectnow_migration_check, public;
CREATE TABLE inspectnow_migration_check."InspectionTemplates" (
    "Id" uuid NOT NULL CONSTRAINT "PK_InspectionTemplates" PRIMARY KEY,
    "Name" varchar(200) NOT NULL, "Status" varchar(20) NOT NULL, "Version" uuid NOT NULL);
CREATE TABLE inspectnow_migration_check."TemplateQuestions" (
    "Id" uuid NOT NULL CONSTRAINT "PK_TemplateQuestions" PRIMARY KEY,
    "Text" varchar(500) NOT NULL, "IsRequired" boolean NOT NULL,
    "InspectionTemplateId" uuid NOT NULL,
    CONSTRAINT "FK_TemplateQuestions_InspectionTemplates_InspectionTemplateId"
        FOREIGN KEY ("InspectionTemplateId") REFERENCES inspectnow_migration_check."InspectionTemplates" ON DELETE CASCADE);
CREATE INDEX "IX_TemplateQuestions_InspectionTemplateId"
    ON inspectnow_migration_check."TemplateQuestions" ("InspectionTemplateId");
CREATE TABLE inspectnow_migration_check."Inspections" (
    "Id" uuid NOT NULL CONSTRAINT "PK_Inspections" PRIMARY KEY,
    "TemplateId" uuid NOT NULL, "TemplateVersion" uuid NOT NULL,
    "TemplateName" varchar(200) NOT NULL, "SiteName" varchar(200) NOT NULL,
    "Status" varchar(20) NOT NULL, "StartedAtUtc" timestamptz NOT NULL, "Version" uuid NOT NULL,
    CONSTRAINT "FK_Inspections_InspectionTemplates_TemplateId"
        FOREIGN KEY ("TemplateId") REFERENCES inspectnow_migration_check."InspectionTemplates" ON DELETE RESTRICT);
CREATE INDEX "IX_Inspections_TemplateId" ON inspectnow_migration_check."Inspections" ("TemplateId");
CREATE TABLE inspectnow_migration_check."InspectionQuestions" (
    "Id" uuid NOT NULL CONSTRAINT "PK_InspectionQuestions" PRIMARY KEY,
    "SourceQuestionId" uuid NOT NULL, "Text" varchar(500) NOT NULL,
    "IsRequired" boolean NOT NULL, "Position" integer NOT NULL, "InspectionId" uuid NOT NULL,
    CONSTRAINT "FK_InspectionQuestions_Inspections_InspectionId"
        FOREIGN KEY ("InspectionId") REFERENCES inspectnow_migration_check."Inspections" ON DELETE CASCADE);
CREATE UNIQUE INDEX "IX_InspectionQuestions_InspectionId_Position"
    ON inspectnow_migration_check."InspectionQuestions" ("InspectionId", "Position");
CREATE UNIQUE INDEX "IX_InspectionQuestions_InspectionId_SourceQuestionId"
    ON inspectnow_migration_check."InspectionQuestions" ("InspectionId", "SourceQuestionId");
INSERT INTO inspectnow_migration_check."InspectionTemplates" VALUES
('10000000-0000-0000-0000-000000000002','Published template','Published','90000000-0000-0000-0000-000000000001'),
('10000000-0000-0000-0000-000000000001','Draft template','Draft','90000000-0000-0000-0000-000000000002');
INSERT INTO inspectnow_migration_check."TemplateQuestions" VALUES
('20000000-0000-0000-0000-000000000001','Floor condition',true,'10000000-0000-0000-0000-000000000002'),
('20000000-0000-0000-0000-000000000002','Draft question',false,'10000000-0000-0000-0000-000000000001');
INSERT INTO inspectnow_migration_check."Inspections" VALUES
('30000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000002',
 '90000000-0000-0000-0000-000000000001','Historical template name','Lahore','Draft',
 '2026-10-06T07:00:00Z','90000000-0000-0000-0000-000000000003'),
('30000000-0000-0000-0000-000000000002','10000000-0000-0000-0000-000000000002',
 '90000000-0000-0000-0000-000000000001','Historical template name','Islamabad','Draft',
 '2026-10-06T08:00:00Z','90000000-0000-0000-0000-000000000004');
INSERT INTO inspectnow_migration_check."InspectionQuestions" VALUES
('40000000-0000-0000-0000-000000000001','20000000-0000-0000-0000-000000000001',
 'Original floor wording',true,1,'30000000-0000-0000-0000-000000000001'),
('40000000-0000-0000-0000-000000000002','20000000-0000-0000-0000-000000000001',
 'Original floor wording',true,1,'30000000-0000-0000-0000-000000000002'),
-- A historical source question no longer present in the source table.
('40000000-0000-0000-0000-000000000003','20000000-0000-0000-0000-000000000003',
 'Archived source question',false,2,'30000000-0000-0000-0000-000000000002');

CREATE TEMP TABLE before_templates ON COMMIT DROP AS SELECT * FROM inspectnow_migration_check."InspectionTemplates";
CREATE TEMP TABLE before_questions ON COMMIT DROP AS SELECT * FROM inspectnow_migration_check."TemplateQuestions";
CREATE TEMP TABLE before_inspections ON COMMIT DROP AS SELECT * FROM inspectnow_migration_check."Inspections";
CREATE TEMP TABLE before_snapshots ON COMMIT DROP AS SELECT * FROM inspectnow_migration_check."InspectionQuestions";

-- Executed inside the EF migration transaction. Stop API writes first.
SET LOCAL lock_timeout = '15s';
LOCK TABLE inspectnow_migration_check."InspectionTemplates", inspectnow_migration_check."TemplateQuestions",
    inspectnow_migration_check."Inspections", inspectnow_migration_check."InspectionQuestions" IN ACCESS EXCLUSIVE MODE;

-- Historical conversion audit only; EF does not map this table.
-- UUIDs here are old identifiers, never active entity primary keys.
CREATE TABLE inspectnow_migration_check."__LegacyIdMap" (
    "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    "EntityTable" text NOT NULL,
    "OldId" uuid NOT NULL,
    "NewId" bigint NOT NULL,
    CONSTRAINT "UQ_LegacyIdMap_Old" UNIQUE ("EntityTable", "OldId"),
    CONSTRAINT "UQ_LegacyIdMap_New" UNIQUE ("EntityTable", "NewId")
);

CREATE TEMP TABLE "InspectNow_IdConversionCounts" (
    "EntityTable" text PRIMARY KEY, "RowCount" bigint NOT NULL
) ON COMMIT DROP;

INSERT INTO "InspectNow_IdConversionCounts"
SELECT 'InspectionTemplates', count(*) FROM inspectnow_migration_check."InspectionTemplates";

INSERT INTO "InspectNow_IdConversionCounts"
SELECT 'TemplateQuestions', count(*) FROM inspectnow_migration_check."TemplateQuestions";

INSERT INTO "InspectNow_IdConversionCounts"
SELECT 'Inspections', count(*) FROM inspectnow_migration_check."Inspections";

INSERT INTO "InspectNow_IdConversionCounts"
SELECT 'InspectionQuestions', count(*) FROM inspectnow_migration_check."InspectionQuestions";

INSERT INTO inspectnow_migration_check."__LegacyIdMap" ("EntityTable", "OldId", "NewId")
SELECT 'InspectionTemplates', "OldId", row_number() OVER (ORDER BY "OldId")
FROM (
    SELECT "Id" AS "OldId" FROM inspectnow_migration_check."InspectionTemplates"
) AS source_ids;

INSERT INTO inspectnow_migration_check."__LegacyIdMap" ("EntityTable", "OldId", "NewId")
SELECT 'TemplateQuestions', "OldId", row_number() OVER (ORDER BY "OldId")
FROM (
    SELECT "Id" AS "OldId" FROM inspectnow_migration_check."TemplateQuestions"
    UNION
    SELECT "SourceQuestionId" FROM inspectnow_migration_check."InspectionQuestions"
) AS source_ids;

INSERT INTO inspectnow_migration_check."__LegacyIdMap" ("EntityTable", "OldId", "NewId")
SELECT 'Inspections', "OldId", row_number() OVER (ORDER BY "OldId")
FROM (
    SELECT "Id" AS "OldId" FROM inspectnow_migration_check."Inspections"
) AS source_ids;

INSERT INTO inspectnow_migration_check."__LegacyIdMap" ("EntityTable", "OldId", "NewId")
SELECT 'InspectionQuestions', "OldId", row_number() OVER (ORDER BY "OldId")
FROM (
    SELECT "Id" AS "OldId" FROM inspectnow_migration_check."InspectionQuestions"
) AS source_ids;

ALTER TABLE inspectnow_migration_check."InspectionTemplates" ADD COLUMN "Id_New" bigint;
UPDATE inspectnow_migration_check."InspectionTemplates" AS entity
SET "Id_New" = mapping."NewId"
FROM inspectnow_migration_check."__LegacyIdMap" AS mapping
WHERE mapping."EntityTable" = 'InspectionTemplates' AND mapping."OldId" = entity."Id";
ALTER TABLE inspectnow_migration_check."InspectionTemplates" ALTER COLUMN "Id_New" SET NOT NULL;

ALTER TABLE inspectnow_migration_check."TemplateQuestions" ADD COLUMN "Id_New" bigint;
UPDATE inspectnow_migration_check."TemplateQuestions" AS entity
SET "Id_New" = mapping."NewId"
FROM inspectnow_migration_check."__LegacyIdMap" AS mapping
WHERE mapping."EntityTable" = 'TemplateQuestions' AND mapping."OldId" = entity."Id";
ALTER TABLE inspectnow_migration_check."TemplateQuestions" ALTER COLUMN "Id_New" SET NOT NULL;

ALTER TABLE inspectnow_migration_check."Inspections" ADD COLUMN "Id_New" bigint;
UPDATE inspectnow_migration_check."Inspections" AS entity
SET "Id_New" = mapping."NewId"
FROM inspectnow_migration_check."__LegacyIdMap" AS mapping
WHERE mapping."EntityTable" = 'Inspections' AND mapping."OldId" = entity."Id";
ALTER TABLE inspectnow_migration_check."Inspections" ALTER COLUMN "Id_New" SET NOT NULL;

ALTER TABLE inspectnow_migration_check."InspectionQuestions" ADD COLUMN "Id_New" bigint;
UPDATE inspectnow_migration_check."InspectionQuestions" AS entity
SET "Id_New" = mapping."NewId"
FROM inspectnow_migration_check."__LegacyIdMap" AS mapping
WHERE mapping."EntityTable" = 'InspectionQuestions' AND mapping."OldId" = entity."Id";
ALTER TABLE inspectnow_migration_check."InspectionQuestions" ALTER COLUMN "Id_New" SET NOT NULL;

ALTER TABLE inspectnow_migration_check."TemplateQuestions" ADD COLUMN "InspectionTemplateId_New" bigint;
UPDATE inspectnow_migration_check."TemplateQuestions" AS entity
SET "InspectionTemplateId_New" = mapping."NewId"
FROM inspectnow_migration_check."__LegacyIdMap" AS mapping
WHERE mapping."EntityTable" = 'InspectionTemplates' AND mapping."OldId" = entity."InspectionTemplateId";
-- Abort rather than lose an unmapped relationship.
ALTER TABLE inspectnow_migration_check."TemplateQuestions" ALTER COLUMN "InspectionTemplateId_New" SET NOT NULL;

ALTER TABLE inspectnow_migration_check."Inspections" ADD COLUMN "TemplateId_New" bigint;
UPDATE inspectnow_migration_check."Inspections" AS entity
SET "TemplateId_New" = mapping."NewId"
FROM inspectnow_migration_check."__LegacyIdMap" AS mapping
WHERE mapping."EntityTable" = 'InspectionTemplates' AND mapping."OldId" = entity."TemplateId";
-- Abort rather than lose an unmapped relationship.
ALTER TABLE inspectnow_migration_check."Inspections" ALTER COLUMN "TemplateId_New" SET NOT NULL;

ALTER TABLE inspectnow_migration_check."InspectionQuestions" ADD COLUMN "InspectionId_New" bigint;
UPDATE inspectnow_migration_check."InspectionQuestions" AS entity
SET "InspectionId_New" = mapping."NewId"
FROM inspectnow_migration_check."__LegacyIdMap" AS mapping
WHERE mapping."EntityTable" = 'Inspections' AND mapping."OldId" = entity."InspectionId";
-- Abort rather than lose an unmapped relationship.
ALTER TABLE inspectnow_migration_check."InspectionQuestions" ALTER COLUMN "InspectionId_New" SET NOT NULL;

ALTER TABLE inspectnow_migration_check."InspectionQuestions" ADD COLUMN "SourceQuestionId_New" bigint;
UPDATE inspectnow_migration_check."InspectionQuestions" AS entity
SET "SourceQuestionId_New" = mapping."NewId"
FROM inspectnow_migration_check."__LegacyIdMap" AS mapping
WHERE mapping."EntityTable" = 'TemplateQuestions' AND mapping."OldId" = entity."SourceQuestionId";
-- Abort rather than lose an unmapped relationship.
ALTER TABLE inspectnow_migration_check."InspectionQuestions" ALTER COLUMN "SourceQuestionId_New" SET NOT NULL;

ALTER TABLE inspectnow_migration_check."TemplateQuestions" DROP CONSTRAINT "FK_TemplateQuestions_InspectionTemplates_InspectionTemplateId";

ALTER TABLE inspectnow_migration_check."Inspections" DROP CONSTRAINT "FK_Inspections_InspectionTemplates_TemplateId";

ALTER TABLE inspectnow_migration_check."InspectionQuestions" DROP CONSTRAINT "FK_InspectionQuestions_Inspections_InspectionId";

ALTER TABLE inspectnow_migration_check."InspectionTemplates" DROP CONSTRAINT "PK_InspectionTemplates";

ALTER TABLE inspectnow_migration_check."TemplateQuestions" DROP CONSTRAINT "PK_TemplateQuestions";

ALTER TABLE inspectnow_migration_check."Inspections" DROP CONSTRAINT "PK_Inspections";

ALTER TABLE inspectnow_migration_check."InspectionQuestions" DROP CONSTRAINT "PK_InspectionQuestions";

ALTER TABLE inspectnow_migration_check."TemplateQuestions" DROP COLUMN "InspectionTemplateId";
ALTER TABLE inspectnow_migration_check."TemplateQuestions" RENAME COLUMN "InspectionTemplateId_New" TO "InspectionTemplateId";

ALTER TABLE inspectnow_migration_check."Inspections" DROP COLUMN "TemplateId";
ALTER TABLE inspectnow_migration_check."Inspections" RENAME COLUMN "TemplateId_New" TO "TemplateId";

ALTER TABLE inspectnow_migration_check."InspectionQuestions" DROP COLUMN "InspectionId";
ALTER TABLE inspectnow_migration_check."InspectionQuestions" RENAME COLUMN "InspectionId_New" TO "InspectionId";

ALTER TABLE inspectnow_migration_check."InspectionQuestions" DROP COLUMN "SourceQuestionId";
ALTER TABLE inspectnow_migration_check."InspectionQuestions" RENAME COLUMN "SourceQuestionId_New" TO "SourceQuestionId";

ALTER TABLE inspectnow_migration_check."InspectionTemplates" DROP COLUMN "Id";
ALTER TABLE inspectnow_migration_check."InspectionTemplates" RENAME COLUMN "Id_New" TO "Id";
ALTER TABLE inspectnow_migration_check."InspectionTemplates" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE inspectnow_migration_check."InspectionTemplates" ADD CONSTRAINT "PK_InspectionTemplates" PRIMARY KEY ("Id");
-- Reserve IDs for historical source questions too, even if their source was deleted.
SELECT setval(
    pg_get_serial_sequence('inspectnow_migration_check."InspectionTemplates"', 'Id')::regclass,
    GREATEST(COALESCE(max("NewId"), 0), 1),
    count(*) > 0)
FROM inspectnow_migration_check."__LegacyIdMap" WHERE "EntityTable" = 'InspectionTemplates';

ALTER TABLE inspectnow_migration_check."TemplateQuestions" DROP COLUMN "Id";
ALTER TABLE inspectnow_migration_check."TemplateQuestions" RENAME COLUMN "Id_New" TO "Id";
ALTER TABLE inspectnow_migration_check."TemplateQuestions" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE inspectnow_migration_check."TemplateQuestions" ADD CONSTRAINT "PK_TemplateQuestions" PRIMARY KEY ("Id");
-- Reserve IDs for historical source questions too, even if their source was deleted.
SELECT setval(
    pg_get_serial_sequence('inspectnow_migration_check."TemplateQuestions"', 'Id')::regclass,
    GREATEST(COALESCE(max("NewId"), 0), 1),
    count(*) > 0)
FROM inspectnow_migration_check."__LegacyIdMap" WHERE "EntityTable" = 'TemplateQuestions';

ALTER TABLE inspectnow_migration_check."Inspections" DROP COLUMN "Id";
ALTER TABLE inspectnow_migration_check."Inspections" RENAME COLUMN "Id_New" TO "Id";
ALTER TABLE inspectnow_migration_check."Inspections" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE inspectnow_migration_check."Inspections" ADD CONSTRAINT "PK_Inspections" PRIMARY KEY ("Id");
-- Reserve IDs for historical source questions too, even if their source was deleted.
SELECT setval(
    pg_get_serial_sequence('inspectnow_migration_check."Inspections"', 'Id')::regclass,
    GREATEST(COALESCE(max("NewId"), 0), 1),
    count(*) > 0)
FROM inspectnow_migration_check."__LegacyIdMap" WHERE "EntityTable" = 'Inspections';

ALTER TABLE inspectnow_migration_check."InspectionQuestions" DROP COLUMN "Id";
ALTER TABLE inspectnow_migration_check."InspectionQuestions" RENAME COLUMN "Id_New" TO "Id";
ALTER TABLE inspectnow_migration_check."InspectionQuestions" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE inspectnow_migration_check."InspectionQuestions" ADD CONSTRAINT "PK_InspectionQuestions" PRIMARY KEY ("Id");
-- Reserve IDs for historical source questions too, even if their source was deleted.
SELECT setval(
    pg_get_serial_sequence('inspectnow_migration_check."InspectionQuestions"', 'Id')::regclass,
    GREATEST(COALESCE(max("NewId"), 0), 1),
    count(*) > 0)
FROM inspectnow_migration_check."__LegacyIdMap" WHERE "EntityTable" = 'InspectionQuestions';

ALTER TABLE inspectnow_migration_check."TemplateQuestions" ADD CONSTRAINT "FK_TemplateQuestions_InspectionTemplates_InspectionTemplateId"
FOREIGN KEY ("InspectionTemplateId") REFERENCES inspectnow_migration_check."InspectionTemplates" ("Id") ON DELETE CASCADE;

ALTER TABLE inspectnow_migration_check."Inspections" ADD CONSTRAINT "FK_Inspections_InspectionTemplates_TemplateId"
FOREIGN KEY ("TemplateId") REFERENCES inspectnow_migration_check."InspectionTemplates" ("Id") ON DELETE RESTRICT;

ALTER TABLE inspectnow_migration_check."InspectionQuestions" ADD CONSTRAINT "FK_InspectionQuestions_Inspections_InspectionId"
FOREIGN KEY ("InspectionId") REFERENCES inspectnow_migration_check."Inspections" ("Id") ON DELETE CASCADE;

CREATE INDEX "IX_TemplateQuestions_InspectionTemplateId"
    ON inspectnow_migration_check."TemplateQuestions" ("InspectionTemplateId");
CREATE INDEX "IX_Inspections_TemplateId" ON inspectnow_migration_check."Inspections" ("TemplateId");
CREATE UNIQUE INDEX "IX_InspectionQuestions_InspectionId_Position"
    ON inspectnow_migration_check."InspectionQuestions" ("InspectionId", "Position");
CREATE UNIQUE INDEX "IX_InspectionQuestions_InspectionId_SourceQuestionId"
    ON inspectnow_migration_check."InspectionQuestions" ("InspectionId", "SourceQuestionId");

-- Previously implemented domain properties, not yet migrated in the uploaded source.
ALTER TABLE inspectnow_migration_check."InspectionQuestions"
    ADD COLUMN "Answer" character varying(20) NULL,
    ADD COLUMN "Comment" character varying(2000) NULL;

DO $verify$
DECLARE
    entry record;
    actual_count bigint;
BEGIN
    FOR entry IN SELECT * FROM "InspectNow_IdConversionCounts" LOOP
        EXECUTE format('SELECT count(*) FROM inspectnow_migration_check.%I', entry."EntityTable")
            INTO actual_count;
        IF actual_count <> entry."RowCount" THEN
            RAISE EXCEPTION 'Row count changed for %', entry."EntityTable";
        END IF;
    END LOOP;
END;
$verify$;

DO $check$
BEGIN
    IF EXISTS (
        SELECT 1 FROM before_templates old
        JOIN inspectnow_migration_check."__LegacyIdMap" mapping
            ON mapping."EntityTable" = 'InspectionTemplates' AND mapping."OldId" = old."Id"
        LEFT JOIN inspectnow_migration_check."InspectionTemplates" after_row ON after_row."Id" = mapping."NewId"
        WHERE (to_jsonb(old) - 'Id') IS DISTINCT FROM (to_jsonb(after_row) - 'Id') OR after_row."Id" IS NULL
    ) THEN RAISE EXCEPTION 'Payload or relationship mismatch: InspectionTemplates'; END IF;
    IF EXISTS (
        SELECT 1 FROM before_questions old
        JOIN inspectnow_migration_check."__LegacyIdMap" mapping
            ON mapping."EntityTable" = 'TemplateQuestions' AND mapping."OldId" = old."Id"
        LEFT JOIN inspectnow_migration_check."TemplateQuestions" after_row ON after_row."Id" = mapping."NewId"
        LEFT JOIN inspectnow_migration_check."__LegacyIdMap" ref0
            ON ref0."EntityTable" = 'InspectionTemplates' AND ref0."OldId" = old."InspectionTemplateId"
        WHERE (to_jsonb(old) - 'Id' - 'InspectionTemplateId') IS DISTINCT FROM (to_jsonb(after_row) - 'Id' - 'InspectionTemplateId') OR after_row."Id" IS NULL OR after_row."InspectionTemplateId" IS DISTINCT FROM ref0."NewId"
    ) THEN RAISE EXCEPTION 'Payload or relationship mismatch: TemplateQuestions'; END IF;
    IF EXISTS (
        SELECT 1 FROM before_inspections old
        JOIN inspectnow_migration_check."__LegacyIdMap" mapping
            ON mapping."EntityTable" = 'Inspections' AND mapping."OldId" = old."Id"
        LEFT JOIN inspectnow_migration_check."Inspections" after_row ON after_row."Id" = mapping."NewId"
        LEFT JOIN inspectnow_migration_check."__LegacyIdMap" ref0
            ON ref0."EntityTable" = 'InspectionTemplates' AND ref0."OldId" = old."TemplateId"
        WHERE (to_jsonb(old) - 'Id' - 'TemplateId') IS DISTINCT FROM (to_jsonb(after_row) - 'Id' - 'TemplateId') OR after_row."Id" IS NULL OR after_row."TemplateId" IS DISTINCT FROM ref0."NewId"
    ) THEN RAISE EXCEPTION 'Payload or relationship mismatch: Inspections'; END IF;
    IF EXISTS (
        SELECT 1 FROM before_snapshots old
        JOIN inspectnow_migration_check."__LegacyIdMap" mapping
            ON mapping."EntityTable" = 'InspectionQuestions' AND mapping."OldId" = old."Id"
        LEFT JOIN inspectnow_migration_check."InspectionQuestions" after_row ON after_row."Id" = mapping."NewId"
        LEFT JOIN inspectnow_migration_check."__LegacyIdMap" ref0
            ON ref0."EntityTable" = 'Inspections' AND ref0."OldId" = old."InspectionId"
        LEFT JOIN inspectnow_migration_check."__LegacyIdMap" ref1
            ON ref1."EntityTable" = 'TemplateQuestions' AND ref1."OldId" = old."SourceQuestionId"
        WHERE (to_jsonb(old) - 'Id' - 'InspectionId' - 'SourceQuestionId') IS DISTINCT FROM (to_jsonb(after_row) - 'Id' - 'Answer' - 'Comment' - 'InspectionId' - 'SourceQuestionId') OR after_row."Id" IS NULL OR after_row."InspectionId" IS DISTINCT FROM ref0."NewId" OR after_row."SourceQuestionId" IS DISTINCT FROM ref1."NewId"
    ) THEN RAISE EXCEPTION 'Payload or relationship mismatch: InspectionQuestions'; END IF;
    RAISE NOTICE 'Populated migration: payloads, versions and relationships preserved.';
END;
$check$;
DO $identity$
DECLARE
    template_id bigint;
    question_id bigint;
    inspection_id bigint;
    snapshot_id bigint;
    actual bigint;
BEGIN
    INSERT INTO inspectnow_migration_check."InspectionTemplates" ("Name", "Status", "Version")
        VALUES ('After conversion', 'Published', gen_random_uuid()) RETURNING "Id" INTO template_id;
    INSERT INTO inspectnow_migration_check."TemplateQuestions" ("Text", "IsRequired", "InspectionTemplateId")
        VALUES ('New question', true, template_id) RETURNING "Id" INTO question_id;
    INSERT INTO inspectnow_migration_check."Inspections"
        ("TemplateId", "TemplateVersion", "TemplateName", "SiteName", "Status", "StartedAtUtc", "Version")
        VALUES (template_id, gen_random_uuid(), 'After conversion', 'Test site', 'Draft', now(), gen_random_uuid())
        RETURNING "Id" INTO inspection_id;
    INSERT INTO inspectnow_migration_check."InspectionQuestions"
        ("SourceQuestionId", "Text", "IsRequired", "Position", "InspectionId", "Answer", "Comment")
        VALUES (question_id, 'New question', true, 1, inspection_id, 'Pass', 'Recorded after migration')
        RETURNING "Id" INTO snapshot_id;
    FOR actual IN SELECT template_id UNION ALL SELECT question_id UNION ALL SELECT inspection_id UNION ALL SELECT snapshot_id LOOP
        IF actual IS NULL OR actual <= 0 THEN RAISE EXCEPTION 'Identity generation failed'; END IF;
    END LOOP;
    IF question_id <= COALESCE((SELECT max("NewId") FROM inspectnow_migration_check."__LegacyIdMap"
        WHERE "EntityTable" = 'TemplateQuestions'), 0) THEN
        RAISE EXCEPTION 'Question sequence reused a reserved historical ID';
    END IF;
    RAISE NOTICE 'New numeric inserts and answer columns passed.';
END;
$identity$;
ROLLBACK;

-- Empty database scenario
BEGIN;
CREATE SCHEMA inspectnow_migration_check;
SET LOCAL search_path TO inspectnow_migration_check, public;
CREATE TABLE inspectnow_migration_check."InspectionTemplates" (
    "Id" uuid NOT NULL CONSTRAINT "PK_InspectionTemplates" PRIMARY KEY,
    "Name" varchar(200) NOT NULL, "Status" varchar(20) NOT NULL, "Version" uuid NOT NULL);
CREATE TABLE inspectnow_migration_check."TemplateQuestions" (
    "Id" uuid NOT NULL CONSTRAINT "PK_TemplateQuestions" PRIMARY KEY,
    "Text" varchar(500) NOT NULL, "IsRequired" boolean NOT NULL,
    "InspectionTemplateId" uuid NOT NULL,
    CONSTRAINT "FK_TemplateQuestions_InspectionTemplates_InspectionTemplateId"
        FOREIGN KEY ("InspectionTemplateId") REFERENCES inspectnow_migration_check."InspectionTemplates" ON DELETE CASCADE);
CREATE INDEX "IX_TemplateQuestions_InspectionTemplateId"
    ON inspectnow_migration_check."TemplateQuestions" ("InspectionTemplateId");
CREATE TABLE inspectnow_migration_check."Inspections" (
    "Id" uuid NOT NULL CONSTRAINT "PK_Inspections" PRIMARY KEY,
    "TemplateId" uuid NOT NULL, "TemplateVersion" uuid NOT NULL,
    "TemplateName" varchar(200) NOT NULL, "SiteName" varchar(200) NOT NULL,
    "Status" varchar(20) NOT NULL, "StartedAtUtc" timestamptz NOT NULL, "Version" uuid NOT NULL,
    CONSTRAINT "FK_Inspections_InspectionTemplates_TemplateId"
        FOREIGN KEY ("TemplateId") REFERENCES inspectnow_migration_check."InspectionTemplates" ON DELETE RESTRICT);
CREATE INDEX "IX_Inspections_TemplateId" ON inspectnow_migration_check."Inspections" ("TemplateId");
CREATE TABLE inspectnow_migration_check."InspectionQuestions" (
    "Id" uuid NOT NULL CONSTRAINT "PK_InspectionQuestions" PRIMARY KEY,
    "SourceQuestionId" uuid NOT NULL, "Text" varchar(500) NOT NULL,
    "IsRequired" boolean NOT NULL, "Position" integer NOT NULL, "InspectionId" uuid NOT NULL,
    CONSTRAINT "FK_InspectionQuestions_Inspections_InspectionId"
        FOREIGN KEY ("InspectionId") REFERENCES inspectnow_migration_check."Inspections" ON DELETE CASCADE);
CREATE UNIQUE INDEX "IX_InspectionQuestions_InspectionId_Position"
    ON inspectnow_migration_check."InspectionQuestions" ("InspectionId", "Position");
CREATE UNIQUE INDEX "IX_InspectionQuestions_InspectionId_SourceQuestionId"
    ON inspectnow_migration_check."InspectionQuestions" ("InspectionId", "SourceQuestionId");
-- Executed inside the EF migration transaction. Stop API writes first.
SET LOCAL lock_timeout = '15s';
LOCK TABLE inspectnow_migration_check."InspectionTemplates", inspectnow_migration_check."TemplateQuestions",
    inspectnow_migration_check."Inspections", inspectnow_migration_check."InspectionQuestions" IN ACCESS EXCLUSIVE MODE;

-- Historical conversion audit only; EF does not map this table.
-- UUIDs here are old identifiers, never active entity primary keys.
CREATE TABLE inspectnow_migration_check."__LegacyIdMap" (
    "Id" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    "EntityTable" text NOT NULL,
    "OldId" uuid NOT NULL,
    "NewId" bigint NOT NULL,
    CONSTRAINT "UQ_LegacyIdMap_Old" UNIQUE ("EntityTable", "OldId"),
    CONSTRAINT "UQ_LegacyIdMap_New" UNIQUE ("EntityTable", "NewId")
);

CREATE TEMP TABLE "InspectNow_IdConversionCounts" (
    "EntityTable" text PRIMARY KEY, "RowCount" bigint NOT NULL
) ON COMMIT DROP;

INSERT INTO "InspectNow_IdConversionCounts"
SELECT 'InspectionTemplates', count(*) FROM inspectnow_migration_check."InspectionTemplates";

INSERT INTO "InspectNow_IdConversionCounts"
SELECT 'TemplateQuestions', count(*) FROM inspectnow_migration_check."TemplateQuestions";

INSERT INTO "InspectNow_IdConversionCounts"
SELECT 'Inspections', count(*) FROM inspectnow_migration_check."Inspections";

INSERT INTO "InspectNow_IdConversionCounts"
SELECT 'InspectionQuestions', count(*) FROM inspectnow_migration_check."InspectionQuestions";

INSERT INTO inspectnow_migration_check."__LegacyIdMap" ("EntityTable", "OldId", "NewId")
SELECT 'InspectionTemplates', "OldId", row_number() OVER (ORDER BY "OldId")
FROM (
    SELECT "Id" AS "OldId" FROM inspectnow_migration_check."InspectionTemplates"
) AS source_ids;

INSERT INTO inspectnow_migration_check."__LegacyIdMap" ("EntityTable", "OldId", "NewId")
SELECT 'TemplateQuestions', "OldId", row_number() OVER (ORDER BY "OldId")
FROM (
    SELECT "Id" AS "OldId" FROM inspectnow_migration_check."TemplateQuestions"
    UNION
    SELECT "SourceQuestionId" FROM inspectnow_migration_check."InspectionQuestions"
) AS source_ids;

INSERT INTO inspectnow_migration_check."__LegacyIdMap" ("EntityTable", "OldId", "NewId")
SELECT 'Inspections', "OldId", row_number() OVER (ORDER BY "OldId")
FROM (
    SELECT "Id" AS "OldId" FROM inspectnow_migration_check."Inspections"
) AS source_ids;

INSERT INTO inspectnow_migration_check."__LegacyIdMap" ("EntityTable", "OldId", "NewId")
SELECT 'InspectionQuestions', "OldId", row_number() OVER (ORDER BY "OldId")
FROM (
    SELECT "Id" AS "OldId" FROM inspectnow_migration_check."InspectionQuestions"
) AS source_ids;

ALTER TABLE inspectnow_migration_check."InspectionTemplates" ADD COLUMN "Id_New" bigint;
UPDATE inspectnow_migration_check."InspectionTemplates" AS entity
SET "Id_New" = mapping."NewId"
FROM inspectnow_migration_check."__LegacyIdMap" AS mapping
WHERE mapping."EntityTable" = 'InspectionTemplates' AND mapping."OldId" = entity."Id";
ALTER TABLE inspectnow_migration_check."InspectionTemplates" ALTER COLUMN "Id_New" SET NOT NULL;

ALTER TABLE inspectnow_migration_check."TemplateQuestions" ADD COLUMN "Id_New" bigint;
UPDATE inspectnow_migration_check."TemplateQuestions" AS entity
SET "Id_New" = mapping."NewId"
FROM inspectnow_migration_check."__LegacyIdMap" AS mapping
WHERE mapping."EntityTable" = 'TemplateQuestions' AND mapping."OldId" = entity."Id";
ALTER TABLE inspectnow_migration_check."TemplateQuestions" ALTER COLUMN "Id_New" SET NOT NULL;

ALTER TABLE inspectnow_migration_check."Inspections" ADD COLUMN "Id_New" bigint;
UPDATE inspectnow_migration_check."Inspections" AS entity
SET "Id_New" = mapping."NewId"
FROM inspectnow_migration_check."__LegacyIdMap" AS mapping
WHERE mapping."EntityTable" = 'Inspections' AND mapping."OldId" = entity."Id";
ALTER TABLE inspectnow_migration_check."Inspections" ALTER COLUMN "Id_New" SET NOT NULL;

ALTER TABLE inspectnow_migration_check."InspectionQuestions" ADD COLUMN "Id_New" bigint;
UPDATE inspectnow_migration_check."InspectionQuestions" AS entity
SET "Id_New" = mapping."NewId"
FROM inspectnow_migration_check."__LegacyIdMap" AS mapping
WHERE mapping."EntityTable" = 'InspectionQuestions' AND mapping."OldId" = entity."Id";
ALTER TABLE inspectnow_migration_check."InspectionQuestions" ALTER COLUMN "Id_New" SET NOT NULL;

ALTER TABLE inspectnow_migration_check."TemplateQuestions" ADD COLUMN "InspectionTemplateId_New" bigint;
UPDATE inspectnow_migration_check."TemplateQuestions" AS entity
SET "InspectionTemplateId_New" = mapping."NewId"
FROM inspectnow_migration_check."__LegacyIdMap" AS mapping
WHERE mapping."EntityTable" = 'InspectionTemplates' AND mapping."OldId" = entity."InspectionTemplateId";
-- Abort rather than lose an unmapped relationship.
ALTER TABLE inspectnow_migration_check."TemplateQuestions" ALTER COLUMN "InspectionTemplateId_New" SET NOT NULL;

ALTER TABLE inspectnow_migration_check."Inspections" ADD COLUMN "TemplateId_New" bigint;
UPDATE inspectnow_migration_check."Inspections" AS entity
SET "TemplateId_New" = mapping."NewId"
FROM inspectnow_migration_check."__LegacyIdMap" AS mapping
WHERE mapping."EntityTable" = 'InspectionTemplates' AND mapping."OldId" = entity."TemplateId";
-- Abort rather than lose an unmapped relationship.
ALTER TABLE inspectnow_migration_check."Inspections" ALTER COLUMN "TemplateId_New" SET NOT NULL;

ALTER TABLE inspectnow_migration_check."InspectionQuestions" ADD COLUMN "InspectionId_New" bigint;
UPDATE inspectnow_migration_check."InspectionQuestions" AS entity
SET "InspectionId_New" = mapping."NewId"
FROM inspectnow_migration_check."__LegacyIdMap" AS mapping
WHERE mapping."EntityTable" = 'Inspections' AND mapping."OldId" = entity."InspectionId";
-- Abort rather than lose an unmapped relationship.
ALTER TABLE inspectnow_migration_check."InspectionQuestions" ALTER COLUMN "InspectionId_New" SET NOT NULL;

ALTER TABLE inspectnow_migration_check."InspectionQuestions" ADD COLUMN "SourceQuestionId_New" bigint;
UPDATE inspectnow_migration_check."InspectionQuestions" AS entity
SET "SourceQuestionId_New" = mapping."NewId"
FROM inspectnow_migration_check."__LegacyIdMap" AS mapping
WHERE mapping."EntityTable" = 'TemplateQuestions' AND mapping."OldId" = entity."SourceQuestionId";
-- Abort rather than lose an unmapped relationship.
ALTER TABLE inspectnow_migration_check."InspectionQuestions" ALTER COLUMN "SourceQuestionId_New" SET NOT NULL;

ALTER TABLE inspectnow_migration_check."TemplateQuestions" DROP CONSTRAINT "FK_TemplateQuestions_InspectionTemplates_InspectionTemplateId";

ALTER TABLE inspectnow_migration_check."Inspections" DROP CONSTRAINT "FK_Inspections_InspectionTemplates_TemplateId";

ALTER TABLE inspectnow_migration_check."InspectionQuestions" DROP CONSTRAINT "FK_InspectionQuestions_Inspections_InspectionId";

ALTER TABLE inspectnow_migration_check."InspectionTemplates" DROP CONSTRAINT "PK_InspectionTemplates";

ALTER TABLE inspectnow_migration_check."TemplateQuestions" DROP CONSTRAINT "PK_TemplateQuestions";

ALTER TABLE inspectnow_migration_check."Inspections" DROP CONSTRAINT "PK_Inspections";

ALTER TABLE inspectnow_migration_check."InspectionQuestions" DROP CONSTRAINT "PK_InspectionQuestions";

ALTER TABLE inspectnow_migration_check."TemplateQuestions" DROP COLUMN "InspectionTemplateId";
ALTER TABLE inspectnow_migration_check."TemplateQuestions" RENAME COLUMN "InspectionTemplateId_New" TO "InspectionTemplateId";

ALTER TABLE inspectnow_migration_check."Inspections" DROP COLUMN "TemplateId";
ALTER TABLE inspectnow_migration_check."Inspections" RENAME COLUMN "TemplateId_New" TO "TemplateId";

ALTER TABLE inspectnow_migration_check."InspectionQuestions" DROP COLUMN "InspectionId";
ALTER TABLE inspectnow_migration_check."InspectionQuestions" RENAME COLUMN "InspectionId_New" TO "InspectionId";

ALTER TABLE inspectnow_migration_check."InspectionQuestions" DROP COLUMN "SourceQuestionId";
ALTER TABLE inspectnow_migration_check."InspectionQuestions" RENAME COLUMN "SourceQuestionId_New" TO "SourceQuestionId";

ALTER TABLE inspectnow_migration_check."InspectionTemplates" DROP COLUMN "Id";
ALTER TABLE inspectnow_migration_check."InspectionTemplates" RENAME COLUMN "Id_New" TO "Id";
ALTER TABLE inspectnow_migration_check."InspectionTemplates" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE inspectnow_migration_check."InspectionTemplates" ADD CONSTRAINT "PK_InspectionTemplates" PRIMARY KEY ("Id");
-- Reserve IDs for historical source questions too, even if their source was deleted.
SELECT setval(
    pg_get_serial_sequence('inspectnow_migration_check."InspectionTemplates"', 'Id')::regclass,
    GREATEST(COALESCE(max("NewId"), 0), 1),
    count(*) > 0)
FROM inspectnow_migration_check."__LegacyIdMap" WHERE "EntityTable" = 'InspectionTemplates';

ALTER TABLE inspectnow_migration_check."TemplateQuestions" DROP COLUMN "Id";
ALTER TABLE inspectnow_migration_check."TemplateQuestions" RENAME COLUMN "Id_New" TO "Id";
ALTER TABLE inspectnow_migration_check."TemplateQuestions" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE inspectnow_migration_check."TemplateQuestions" ADD CONSTRAINT "PK_TemplateQuestions" PRIMARY KEY ("Id");
-- Reserve IDs for historical source questions too, even if their source was deleted.
SELECT setval(
    pg_get_serial_sequence('inspectnow_migration_check."TemplateQuestions"', 'Id')::regclass,
    GREATEST(COALESCE(max("NewId"), 0), 1),
    count(*) > 0)
FROM inspectnow_migration_check."__LegacyIdMap" WHERE "EntityTable" = 'TemplateQuestions';

ALTER TABLE inspectnow_migration_check."Inspections" DROP COLUMN "Id";
ALTER TABLE inspectnow_migration_check."Inspections" RENAME COLUMN "Id_New" TO "Id";
ALTER TABLE inspectnow_migration_check."Inspections" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE inspectnow_migration_check."Inspections" ADD CONSTRAINT "PK_Inspections" PRIMARY KEY ("Id");
-- Reserve IDs for historical source questions too, even if their source was deleted.
SELECT setval(
    pg_get_serial_sequence('inspectnow_migration_check."Inspections"', 'Id')::regclass,
    GREATEST(COALESCE(max("NewId"), 0), 1),
    count(*) > 0)
FROM inspectnow_migration_check."__LegacyIdMap" WHERE "EntityTable" = 'Inspections';

ALTER TABLE inspectnow_migration_check."InspectionQuestions" DROP COLUMN "Id";
ALTER TABLE inspectnow_migration_check."InspectionQuestions" RENAME COLUMN "Id_New" TO "Id";
ALTER TABLE inspectnow_migration_check."InspectionQuestions" ALTER COLUMN "Id" ADD GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE inspectnow_migration_check."InspectionQuestions" ADD CONSTRAINT "PK_InspectionQuestions" PRIMARY KEY ("Id");
-- Reserve IDs for historical source questions too, even if their source was deleted.
SELECT setval(
    pg_get_serial_sequence('inspectnow_migration_check."InspectionQuestions"', 'Id')::regclass,
    GREATEST(COALESCE(max("NewId"), 0), 1),
    count(*) > 0)
FROM inspectnow_migration_check."__LegacyIdMap" WHERE "EntityTable" = 'InspectionQuestions';

ALTER TABLE inspectnow_migration_check."TemplateQuestions" ADD CONSTRAINT "FK_TemplateQuestions_InspectionTemplates_InspectionTemplateId"
FOREIGN KEY ("InspectionTemplateId") REFERENCES inspectnow_migration_check."InspectionTemplates" ("Id") ON DELETE CASCADE;

ALTER TABLE inspectnow_migration_check."Inspections" ADD CONSTRAINT "FK_Inspections_InspectionTemplates_TemplateId"
FOREIGN KEY ("TemplateId") REFERENCES inspectnow_migration_check."InspectionTemplates" ("Id") ON DELETE RESTRICT;

ALTER TABLE inspectnow_migration_check."InspectionQuestions" ADD CONSTRAINT "FK_InspectionQuestions_Inspections_InspectionId"
FOREIGN KEY ("InspectionId") REFERENCES inspectnow_migration_check."Inspections" ("Id") ON DELETE CASCADE;

CREATE INDEX "IX_TemplateQuestions_InspectionTemplateId"
    ON inspectnow_migration_check."TemplateQuestions" ("InspectionTemplateId");
CREATE INDEX "IX_Inspections_TemplateId" ON inspectnow_migration_check."Inspections" ("TemplateId");
CREATE UNIQUE INDEX "IX_InspectionQuestions_InspectionId_Position"
    ON inspectnow_migration_check."InspectionQuestions" ("InspectionId", "Position");
CREATE UNIQUE INDEX "IX_InspectionQuestions_InspectionId_SourceQuestionId"
    ON inspectnow_migration_check."InspectionQuestions" ("InspectionId", "SourceQuestionId");

-- Previously implemented domain properties, not yet migrated in the uploaded source.
ALTER TABLE inspectnow_migration_check."InspectionQuestions"
    ADD COLUMN "Answer" character varying(20) NULL,
    ADD COLUMN "Comment" character varying(2000) NULL;

DO $verify$
DECLARE
    entry record;
    actual_count bigint;
BEGIN
    FOR entry IN SELECT * FROM "InspectNow_IdConversionCounts" LOOP
        EXECUTE format('SELECT count(*) FROM inspectnow_migration_check.%I', entry."EntityTable")
            INTO actual_count;
        IF actual_count <> entry."RowCount" THEN
            RAISE EXCEPTION 'Row count changed for %', entry."EntityTable";
        END IF;
    END LOOP;
END;
$verify$;
DO $identity$
DECLARE
    template_id bigint;
    question_id bigint;
    inspection_id bigint;
    snapshot_id bigint;
    actual bigint;
BEGIN
    INSERT INTO inspectnow_migration_check."InspectionTemplates" ("Name", "Status", "Version")
        VALUES ('After conversion', 'Published', gen_random_uuid()) RETURNING "Id" INTO template_id;
    INSERT INTO inspectnow_migration_check."TemplateQuestions" ("Text", "IsRequired", "InspectionTemplateId")
        VALUES ('New question', true, template_id) RETURNING "Id" INTO question_id;
    INSERT INTO inspectnow_migration_check."Inspections"
        ("TemplateId", "TemplateVersion", "TemplateName", "SiteName", "Status", "StartedAtUtc", "Version")
        VALUES (template_id, gen_random_uuid(), 'After conversion', 'Test site', 'Draft', now(), gen_random_uuid())
        RETURNING "Id" INTO inspection_id;
    INSERT INTO inspectnow_migration_check."InspectionQuestions"
        ("SourceQuestionId", "Text", "IsRequired", "Position", "InspectionId", "Answer", "Comment")
        VALUES (question_id, 'New question', true, 1, inspection_id, 'Pass', 'Recorded after migration')
        RETURNING "Id" INTO snapshot_id;
    FOR actual IN SELECT template_id UNION ALL SELECT question_id UNION ALL SELECT inspection_id UNION ALL SELECT snapshot_id LOOP
        IF actual IS NULL OR actual <= 0 THEN RAISE EXCEPTION 'Identity generation failed'; END IF;
    END LOOP;
    IF question_id <= COALESCE((SELECT max("NewId") FROM inspectnow_migration_check."__LegacyIdMap"
        WHERE "EntityTable" = 'TemplateQuestions'), 0) THEN
        RAISE EXCEPTION 'Question sequence reused a reserved historical ID';
    END IF;
    RAISE NOTICE 'New numeric inserts and answer columns passed.';
END;
$identity$;
ROLLBACK;
