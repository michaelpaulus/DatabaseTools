﻿IF NOT EXISTS
    (
        SELECT
            1
        FROM
            sys.indexes INNER JOIN
            sys.tables ON
                indexes.object_id = tables.object_id INNER JOIN
            sys.schemas ON
                tables.schema_id = schemas.schema_id
        WHERE
            indexes.name = 'AK_Store_rowguid' AND
            schemas.name = 'Sales'
    )
    CREATE UNIQUE NONCLUSTERED INDEX [AK_Store_rowguid] ON [Sales].[Store]
    (
    [rowguid]
    )
GO
