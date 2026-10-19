using Chat.Data;
using Chat.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Chat.Hubs
{
    public class ChatHub : Hub
    {
        private readonly IDbContextFactory<ChatDbContext> _contextFactory;

        public ChatHub(IDbContextFactory<ChatDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }
        public async Task SendMessageToRoom(ChatMessage message)
        {
            message.User = "User"; // временно, пока нет аутентификации
            message.SentAt = DateTime.UtcNow;

            // Сохраняем в PostgreSQL
            await using var context = await _contextFactory.CreateDbContextAsync();
            context.Messages.Add(message);
            await context.SaveChangesAsync();

            // Рассылаем всем в комнате
            await Clients.Group(message.Room)
                     .SendAsync("ReceiveMessage", message);
        }

        // Опционально: загрузка истории при входе в комнату
        public async Task<List<ChatMessage>> GetHistory(string room, int limit = 50)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Messages
                .Where(m => m.Room == room)
                .OrderByDescending(m => m.SentAt)
                .Take(limit)
                .OrderBy(m => m.SentAt)
                .ToListAsync();
        }
    }
}