using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class MessageController : MonoBehaviour
{
    private struct Message
	{
        public string message;
        public float timestamp;

		public Message(string message)
		{
			this.message = message;
			this.timestamp = Time.time;
		}
	}

	public static MessageController instance = null;

    [SerializeField] private Text messageField = null;
    [SerializeField] private float messageDuration = 20.0f;
	private Queue<Message> messages = null;

	public static MessageController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		messages = new Queue<Message>();
		instance = this;
	}

	private void Update()
	{
		while(messages.Count > 0 && messages.Peek().timestamp + messageDuration < Time.time)
		{
			messages.Dequeue();
		}

		StringBuilder messageText = new StringBuilder();
		foreach(Message message in messages)
		{
			messageText.AppendLine(message.message);
		}
		messageField.text = messageText.ToString();
	}

	public void AddMessage(string message)
	{
		messages.Enqueue(new Message(message));
	}
}
