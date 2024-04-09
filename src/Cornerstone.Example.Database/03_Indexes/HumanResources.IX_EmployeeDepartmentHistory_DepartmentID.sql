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
            indexes.name = 'IX_EmployeeDepartmentHistory_DepartmentID' AND
            schemas.name = 'HumanResources'
    )
    CREATE NONCLUSTERED INDEX [IX_EmployeeDepartmentHistory_DepartmentID] ON [HumanResources].[EmployeeDepartmentHistory]
    (
    [DepartmentID]
    )
GO
