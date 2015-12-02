using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;


namespace QuantConnect
{
    /// <summary>
    /// The actual message
    /// </summary>
    [DataContract]
    public class Message
    {
        /// <summary>
        /// The Id
        /// </summary>
        [DataMember]
        public int Id { get; set; }
        /// <summary>
        /// A message type so it can map to headers and get an instantiation type
        /// </summary>
        [DataMember]
        [ForeignKey("MessageType")]
        public int MessageTypeId { get; set; }
        /// <summary>
        /// The contents of the message.
        /// </summary>
        [DataMember]
        public string Contents { get; set; }

        /// <summary>
        /// The date and time when the message was sent.  Filled in by the sender
        /// </summary>
        [DataMember]
        public DateTime WhenSent { get; set; }
        /// <summary>
        /// The date and time when the message was received.  Filled in by the Controller
        /// </summary>
        [DataMember]
        public DateTime? WhenReceived { get; set; }
    }
}
