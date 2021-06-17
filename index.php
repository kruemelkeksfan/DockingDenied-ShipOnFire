This is not a real Website.

<?php
	// Taken from https://stackoverflow.com/questions/7895335/append-data-to-a-json-file-with-php

	if(!empty($_GET) && !empty($_GET['json']))
	{
		$oldFeedbackJson = file_get_contents('feedback.json');
		$feedback = json_decode($oldFeedbackJson);
		array_push($feedback, json_decode($_GET['json'], true));
		$newFeedbackJson = json_encode($feedback);
		file_put_contents('feedback.json', $newFeedbackJson);
		print('Success, thank you for your Feedback!');
	}
?>