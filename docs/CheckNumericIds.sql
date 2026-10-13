-- Run after Update-Database, against inspectnow.
SELECT current_database(), current_user;
SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId";

SELECT table_name, column_name, data_type, is_identity, identity_generation
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name IN ('InspectionTemplates','TemplateQuestions','Inspections','InspectionQuestions')
  AND column_name IN ('Id','InspectionTemplateId','TemplateId','InspectionId','SourceQuestionId','Version','TemplateVersion','Answer','Comment')
ORDER BY table_name, ordinal_position;

-- Original GUID -> assigned bigint, including historical source references.
SELECT * FROM public."__LegacyIdMap" ORDER BY "EntityTable", "NewId";

SELECT 'InspectionTemplates' AS "Table", count(*) FROM public."InspectionTemplates"
UNION ALL SELECT 'TemplateQuestions', count(*) FROM public."TemplateQuestions"
UNION ALL SELECT 'Inspections', count(*) FROM public."Inspections"
UNION ALL SELECT 'InspectionQuestions', count(*) FROM public."InspectionQuestions";

-- Each count must be zero; SourceQuestionId intentionally remains a historical
-- reference and is not a foreign key (same behavior as before the migration).
SELECT 'TemplateQuestions -> InspectionTemplates' AS "Relationship", count(*) AS "Orphans"
FROM public."TemplateQuestions" q LEFT JOIN public."InspectionTemplates" t ON t."Id"=q."InspectionTemplateId"
WHERE t."Id" IS NULL
UNION ALL
SELECT 'Inspections -> InspectionTemplates', count(*)
FROM public."Inspections" i LEFT JOIN public."InspectionTemplates" t ON t."Id"=i."TemplateId"
WHERE t."Id" IS NULL
UNION ALL
SELECT 'InspectionQuestions -> Inspections', count(*)
FROM public."InspectionQuestions" q LEFT JOIN public."Inspections" i ON i."Id"=q."InspectionId"
WHERE i."Id" IS NULL;
