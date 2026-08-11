using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.Persistence;

// Generic interface for Infrastructure-level registration
public interface IUnitOfWork<TContext> : IUnitOfWork where TContext : DbContext
{ }