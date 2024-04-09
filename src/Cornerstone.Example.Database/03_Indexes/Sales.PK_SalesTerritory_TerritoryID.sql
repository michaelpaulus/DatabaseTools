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
            indexes.name = 'PK_SalesTerritory_TerritoryID' AND
            schemas.name = 'Sales'
    )
    ALTER TABLE [Sales].[SalesTerritory] ADD CONSTRAINT [PK_SalesTerritory_TerritoryID] PRIMARY KEY CLUSTERED
    (
    [TerritoryID]
    )
GO
