using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("webchat/[controller]")]
public class SignupController : ControllerBase      // requisições HTTP com rota product
{
     private readonly AppDbContext _context;
    private readonly JWTService _jwtService; // Injeta o serviço JWT

    public SignupController(AppDbContext context, JWTService jwtService)     //permite que o controlador acesse o banco de dados.
    {
         _context = context;
        _jwtService = jwtService; // Inicializa o serviço JWT
    }

    [HttpPost] //cria user
    public IActionResult Signup(User user)
    {
        if (user == null || string.IsNullOrEmpty(user.Name) || string.IsNullOrEmpty(user.Password))
        {
            return BadRequest("dados incompletos.");
        }

        var isValid = _context.User.Any(u => u.Name.ToLower() == user.Name.ToLower());

        if (isValid == true) 
        {
            return Conflict("Já existe um usuário com este nome.");
        }

        user.Id = Guid.NewGuid();  // Gera um novo Guid para o ID do usuário
        user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);  // Criptografa a senha
        _context.User.Add(user); //add user ao banco
        _context.SaveChanges(); //salva

         var token = _jwtService.GenerateToken(user.Id, user.Name);   //gera token com id e name
          
      return Ok(new { Token = token });
    }
}

[ApiController]
[Route("webchat/[controller]")]
public class SigninController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly JWTService _jwtService; // Injeta o serviço JWT

    public SigninController(AppDbContext context, JWTService jwtService) // Permite acessar o banco e gerar tokens
    {
        _context = context;
        _jwtService = jwtService; // Inicializa o serviço JWT
    }

    [HttpPost]  //validaçao login
    public IActionResult Signin(User user)
    {
        if (user == null || string.IsNullOrEmpty(user.Name) || string.IsNullOrEmpty(user.Password))
        {
            return BadRequest("dados incompletos.");
        }
       var userFromDb = _context.User.FirstOrDefault(u => u.Name.ToLower() == user.Name.ToLower());   //verifica se user.name de todos é igual a o user name que enviou e retorna ele

        if (userFromDb == null)
        {
            return NotFound("Usuário não encontrado.");
        }

        var passwordMatch = BCrypt.Net.BCrypt.Verify(user.Password, userFromDb.Password);  //verifica se a senha enviada é igual a senha no banco
        if (!passwordMatch)
        {
            return Unauthorized("Senha inválida.");
        }

        var token = _jwtService.GenerateToken(userFromDb.Id, userFromDb.Name);
          
      return Ok(new { Token = token });
        
    }

}
[ApiController]
[Route("webchat/[controller]")]
public class NewController : ControllerBase
{
    private readonly AppDbContext _context;

    public NewController(AppDbContext context) // Permite acessar o banco 
    {
        _context = context;
    }
    [HttpPost]    //cria mensagem
public IActionResult NewMessage(Message message)
{
    if (message == null || string.IsNullOrEmpty(message.Data))
    {
        return BadRequest("Dados incompletos.");
    }

    _context.Message.Add(message);  //add
    _context.SaveChanges(); //salva

    return Ok("mensagem criada com sucesso!");
    }
}

[ApiController]
[Route("webchat/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context) // Permite acessar o banco
    {
        _context = context;
    }

    [HttpGet("{id}")]  //users menos o id que mandou
    public IActionResult GetUsersExceptId(Guid id)
    {
       var users = _context.User.Where(u => u.Id != id).Select(u => new {Id = u.Id, Name = u.Name}
);
        if (users == null)
        {
            return NotFound("Usuários não encontrados.");
        }
        return Ok(users);
    }
}

[ApiController]
[Route("webchat/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly AppDbContext _context;

    public MessagesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("{id}")]
    public IActionResult GetMessage(Guid id)
    {
        var users = _context.User.Where(u => u.Id != id).Select(u => u.Id).ToList();

        var lastMessageForMeList = new List<Message>();
        var lastMessagesMeList = new List<Message>();

        foreach (var userId in users)
        {
            var lastMessageForMe = _context.Message
                                            .Where(m => m.From == userId.ToString() && m.Receive == id.ToString())
                                            .OrderByDescending(m => m.CreatedAt)
                                            .FirstOrDefault();

            var myLastMessage = _context.Message
                                         .Where(m => m.From == id.ToString() && m.Receive == userId.ToString())
                                         .OrderByDescending(m => m.CreatedAt)
                                         .FirstOrDefault();

            if (myLastMessage != null)
            {
                lastMessagesMeList.Add(myLastMessage);
            }
            if (lastMessageForMe != null)
            {
                lastMessageForMeList.Add(lastMessageForMe);
            }
        }

        if (lastMessageForMeList.Count == 0 && lastMessagesMeList.Count == 0)
        {
            return NotFound("Mensagens não encontradas.");
        }

        lastMessageForMeList.AddRange(lastMessagesMeList);

        var lastMessagesDict = new Dictionary<string, (Message message, User userFrom, User userReceive)>();

        foreach (var message in lastMessageForMeList)
        {
            var key = $"{message.From}-{message.Receive}";
            var reverseKey = $"{message.Receive}-{message.From}";

            var userFrom = _context.User.FirstOrDefault(u => u.Id.ToString() == message.From);
            var userReceive = _context.User.FirstOrDefault(u => u.Id.ToString() == message.Receive);

            if (userFrom == null || userReceive == null)
            {
                continue; // Se algum usuário não for encontrado, ignore esta mensagem
            }

            if (lastMessagesDict.ContainsKey(key))
            {
                if (message.CreatedAt > lastMessagesDict[key].message.CreatedAt)
                {
                    lastMessagesDict[key] = (message, userFrom, userReceive);
                }
            }
            else if (lastMessagesDict.ContainsKey(reverseKey))
            {
                if (message.CreatedAt > lastMessagesDict[reverseKey].message.CreatedAt)
                {
                    lastMessagesDict[reverseKey] = (message, userFrom, userReceive);
                }
            }
            else
            {
                lastMessagesDict[key] = (message, userFrom, userReceive);
            }
        }

        var lastMessages = lastMessagesDict.Values
                            .OrderBy(m => m.message.CreatedAt)
                            .Select(m => new
                            {
                                Message = m.message,
                                UserFrom = m.userFrom.Name,
                                UserReceive = m.userReceive.Name
                            })
                            .ToList();

        return Ok(lastMessages);
    }
}




[ApiController] //BUSCA ULTIMA MSG COM TODOS OS IDS
[Route("webchat/[controller]")]
public class ChatController : ControllerBase
{
    private readonly AppDbContext _context;
    public ChatController(AppDbContext context) // Permite acessar o banco
    {
        _context = context;
    }
  [HttpPost]  // mensagem por id
public IActionResult GetChat(Message message)
{

   // Verifica se os IDs são válidos
    var isFromValid = _context.User.Any(u => u.Id.ToString() == message.From);
    var isReceiveValid = _context.User.Any(u => u.Id.ToString() == message.Receive);

    if (!isFromValid || !isReceiveValid)
    {
        return BadRequest("IDs inválidos fornecidos.");
    } 
    // Mensagens enviadas pelo usuário
    var messagesFromMe = _context.Message
                                 .Where(m => m.From == message.From && m.Receive == message.Receive)
                                 .ToList();

    // Mensagens recebidas pelo usuário
    var messagesToMe = _context.Message
                               .Where(m => m.From == message.Receive && m.Receive == message.From)
                               .ToList();

    // Combina todas as mensagens e ordena por data de criação
    var allMessages = messagesFromMe.Concat(messagesToMe)
                                    .OrderBy(o => o.CreatedAt)
                                    .ToList();

    if (allMessages == null)
    {
        return NotFound("Mensagens não encontradas.");
    }

    return Ok(allMessages);
}

    
}