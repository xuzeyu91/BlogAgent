﻿namespace BlogAgent.Domain.Repositories
{
    public interface IEntityService
    {
        bool CreateEntity(string entityName, string filePath);
    }
}
