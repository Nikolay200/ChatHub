using Chat.Models;
using Microsoft.EntityFrameworkCore;

namespace Chat.Data;

public class ChatDbContext : DbContext
{
    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options) { }

    public DbSet<ChatMessage> Messages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.User).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Room).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => new { e.Room, e.SentAt });
        });
    }
}