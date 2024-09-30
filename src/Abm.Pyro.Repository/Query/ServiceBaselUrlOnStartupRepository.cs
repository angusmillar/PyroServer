using Abm.Pyro.Domain.Configuration;
using Abm.Pyro.Domain.Model;
using Abm.Pyro.Domain.Query;
using Abm.Pyro.Repository.DependencyFactory;
using Microsoft.EntityFrameworkCore;

namespace Abm.Pyro.Repository.Query;

public class ServiceBaselUrlOnStartupRepository(
    IPyroDbContextFactory pyroDbContextFactory) : IServiceBaseUrlOnStartupRepository
{
    private PyroDbContext? _context;

    private bool _isUnitOfWorkStarted = false;

    public void StartUnitOfWork(Tenant tenant)
    {
        _context = pyroDbContextFactory.Get(tenant);
        _isUnitOfWorkStarted = true;
    }

    public bool IsUnitOfWorkStarted()
    {
        return _isUnitOfWorkStarted;
    }

    public async Task<int> SaveChangesAsync()
    {
        if (_context is null)
        {
            throw new NullReferenceException(nameof(_context));
        }

        return await _context.SaveChangesAsync();
    }

    public async Task DisposeDbContextAsync()
    {
        if (_context is null)
        {
            throw new NullReferenceException(nameof(_context));
        }
        
        await _context.DisposeAsync();
    }
    
    public async Task<ServiceBaseUrl?> Get(string url)
    {
        if (_context is null)
        {
            throw new NullReferenceException(nameof(_context));
        }
        
        return await _context.Set<ServiceBaseUrl>().SingleOrDefaultAsync(x => x.Url == url);
    }

    public async Task<ServiceBaseUrl?> Get()
    {
        if (_context is null)
        {
            throw new NullReferenceException(nameof(_context));
        }
        
        return await _context.Set<ServiceBaseUrl>().SingleOrDefaultAsync(x => x.IsPrimary == true);
    }

    public ServiceBaseUrl Update(ServiceBaseUrl serviceBaseUrl)
    {
        if (_context is null)
        {
            throw new NullReferenceException(nameof(_context));
        }
        
        _context.Update(serviceBaseUrl);
         
        return serviceBaseUrl;
    }

    public ServiceBaseUrl Add(ServiceBaseUrl serviceBaseUrl)
    {
        if (_context is null)
        {
            throw new NullReferenceException(nameof(_context));
        }
        
        _context.Add(serviceBaseUrl);
        
        return serviceBaseUrl;
    }
}