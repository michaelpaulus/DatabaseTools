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
            indexes.name = 'IX_BusinessEntityAddress_AddressTypeID' AND
            schemas.name = 'Person'
    )
    CREATE NONCLUSTERED INDEX [IX_BusinessEntityAddress_AddressTypeID] ON [Person].[BusinessEntityAddress]
    (
    [AddressTypeID]
    )
GO
