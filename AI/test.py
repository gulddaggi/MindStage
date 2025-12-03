from fastapi import FastAPI, UploadFile, File
import whisper
import tempfile
import os
import shutil


app = FastAPI()
model = whisper.load_model("small")

states = {} #store interview states per user

#get : resume_id, pdf file        : out 을 텍스트로만 해도 되나? (spring에 맡기기)
#out : success / fail, if suc, then store result text to db
@app.post("/api/v1/ocr/{resume_id}")
async def s(resume_id : int, file : UploadFile = File(...)) :
  print(f"Received file: {file.filename}")  # Print the filename of the uploaded file
  with tempfile.NamedTemporaryFile(delete=False, suffix=".pdf") as tmp:
    tmp.write(await file.read())
    tmp_path = tmp.name

  # result = model.transcribe(tmp_path)
  # text = result["text"]
  
  print(f"{resume_id} sent file") #TODO need db access, wav management
  os.remove(tmp_path)
  return {"text": resume_id}

#initially create interview questions for user
#get : 
@app.post("/api/v1/interview/start/{interview_id}/{s3key}")
async def s1() :
  
  pass

#TODO body should contain userid to track state?
#with current db context, generate next question.
@app.post("/api/v1/interview/answer/{interview_id}/{s3key}")
async def s2() :
  
  pass

#
@app.get("/api/v1/interview/end/{interview_id}/{s3key}") #db : interviewid -> userid, 
async def s3() :
  pass