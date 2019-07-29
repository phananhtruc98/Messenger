using System;
using System.Threading.Tasks;
using GrooveMessengerAPI.Areas.Chat.Models;
using GrooveMessengerAPI.Constants;
using GrooveMessengerAPI.Controllers;
using GrooveMessengerAPI.Hubs;
using GrooveMessengerAPI.Hubs.Utils;
using GrooveMessengerAPI.Models;
using GrooveMessengerDAL.Models.CustomModel;
using GrooveMessengerDAL.Models.Message;
using GrooveMessengerDAL.Services.Interface;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace GrooveMessengerAPI.Areas.Chat.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Route("api/[controller]")]
    public class MessageController : ApiControllerBase
    {
        private readonly IContactService _contactService;
        private readonly IMessageService _mesService;
        private readonly HubConnectionStorage _connectionStore;
        private readonly IHubContext<MessageHub, IMessageHubClient> _hubContext;

        public MessageController(
            IMessageService mesService,
            IContactService contactService,
            IUserResolverService userResolver,
            IHubContext<MessageHub, IMessageHubClient> hubContext,
            HubConnectionStorage connectionStore
        ) : base(userResolver)
        {
            _mesService = mesService;
            _contactService = contactService;
            _hubContext = hubContext;
            _connectionStore = connectionStore;
        }

        [HttpGet]
        public IActionResult Get([FromQuery] PagingParameterModel pagingparametermodel)
        {
            if (!ModelState.IsValid) return BadRequest();

            var CurrentPage = pagingparametermodel.pageNumber;
            var PageSize = pagingparametermodel.pageSize;
            var result = _mesService.loadMoreMessages(CurrentPage, PageSize);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public IActionResult GetById(Guid id)
        {
            if (_mesService.GetMessageById(id) != null) return Ok(_mesService.GetMessageById(id));
            return BadRequest();
        }

        [HttpPut("{id}")]
        public EditMessageModel EditMessage(Guid id, [FromBody] EditMessageModel message)
        {
            if (id != message.Id) return null;

            if (ModelState.IsValid)
            {
                var isExisting = _mesService.CheckExisting(id);
                if (!isExisting) return null;

                _mesService.EditMessageModel(message);
                return message;
            }

            return null;
        }

        //Truc: Get UnreadMessageAmount
        [HttpGet("unread/{conversationId}")]
        public async Task<IActionResult> GetUnreadMessages(Guid conversationId)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            //var result = _mesService.GetUnreadMessages(conversationId);
            var contactsList = await _contactService.GetContacts(conversationId);

            foreach (var contact in contactsList)
                foreach (var connectionId in _connectionStore.GetConnections("message", contact.UserName))
                {
                    var unreadMessageAmount = _mesService.GetUnreadMessages(conversationId, contact.Id);
                    var unreadMessageModel = new UnreadMessageModel
                    { ConversationId = conversationId, Amount = unreadMessageAmount };

                    await _hubContext.Clients.Client(connectionId).SendUnreadMessagesAmount(unreadMessageModel);
                }

            return Ok();
        }


        [HttpGet("read/{conversationId}")]
        public IActionResult Get(Guid conversationId)
        {
            if (!ModelState.IsValid) return BadRequest();

            _mesService.SetValueSeenBy(CurrentUserId.ToString(), conversationId);
            foreach (var connectionId in _connectionStore.GetConnections("message", CurrentUserId.ToString()))
            {
                var unreadMessageModel = new UnreadMessageModel { ConversationId = conversationId, Amount = 0 };
                _hubContext.Clients.Client(connectionId).SendUnreadMessagesAmount(unreadMessageModel);
            }

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CreateMessageModel createMessageModel)
        {
            if (!ModelState.IsValid) return BadRequest();

            var createdMessage = await _mesService.AddMessageAsync(createMessageModel); // add message to db
            if (createdMessage != null) // broadcast message to user
            {
                var message = new Message(createdMessage.ConversationId, createdMessage.SenderId, createdMessage.Id,
                    createdMessage.Content, createdMessage.CreatedOn);

                var receiverEmail = await _contactService.GetUserContactEmail(createMessageModel.Receiver);
                foreach (var connectionId in _connectionStore.GetConnections(HubConstant.MessageHubTopic, receiverEmail))
                    await _hubContext.Clients.Client(connectionId).SendMessage(message);
                //Merge: Check if OK
                foreach (var connectionId in _connectionStore.GetConnections("message", CurrentUserName))
                    await _hubContext.Clients.Client(connectionId).SendMessage(message);
                return Ok();
            }

            return NotFound();
        }

        [HttpDelete("{id}")]
        public IActionResult DeleteMessage(Guid id)
        {
            var isExisting = _mesService.CheckExisting(id);
            if (!isExisting) return new NotFoundResult();

            _mesService.DeleteMessage(id);
            return Ok();
        }
    }
}