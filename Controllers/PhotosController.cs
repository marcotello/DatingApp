using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using DatingApp.API.Data;
using DatingApp.API.Dtos;
using DatingApp.API.Helpers;
using DatingApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DatingApp.API.Controllers
{
    [Authorize]
    [Route("api/users/{userId}/photos")]
    public class PhotosController : Controller
    {
        private readonly IDatingRepository _repository;
        private readonly IMapper _mapper;
        private readonly IOptions<CloudinarySettings> _cloudinarySettings;
        private Cloudinary _cloudinary;
        public PhotosController(IDatingRepository repository,
        IMapper mapper,
        IOptions<CloudinarySettings> cloudinarySettings)
        {
            _repository = repository;
            _mapper = mapper;
            _cloudinarySettings = cloudinarySettings;

            Account acc = new Account(
                _cloudinarySettings.Value.CloudName, 
                _cloudinarySettings.Value.ApiKey,
                _cloudinarySettings.Value.ApiSecret
            );

            _cloudinary = new Cloudinary(acc);
        }

        [HttpGet("{id}", Name = "GetPhoto")]
        public async Task<IActionResult> GetPhoto(int id)
        {
            var photoFromRepo = await _repository.GetPhoto(id);

            var photo = _mapper.Map<PhotoForReturnDto>(photoFromRepo);

            return Ok(photo);
        }

        [HttpPost]
        public async Task <IActionResult> AddPhotoForUser(int userId, PhotoForCreation photoForCreation)
        {
            var user = await _repository.GetUser(userId);

            if(user == null)
            {
                return BadRequest("Could not find user");
            }

            var currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            if(currentUserId != user.Id)
            {
                return Unauthorized();
            }

            var file = photoForCreation.File;

            var uploadResult = new ImageUploadResult();

            if(file.Length > 0)
            {
                using (var stream = file.OpenReadStream())
                {
                    var uploadParams = new ImageUploadParams()
                    {
                        File = new FileDescription(file.Name, stream),
                        Transformation = new Transformation().Width(500).Height(500).Crop("fill").Gravity("face")
                    };

                    uploadResult = _cloudinary.Upload(uploadParams);
                }
            }

            photoForCreation.Url = uploadResult.Uri.ToString();
            photoForCreation.PublicId = uploadResult.PublicId;

            var photo = _mapper.Map<Photo>(photoForCreation);
            photo.User = user;

            if(!user.Photos.Any(m => m.IsMain))
            {
                photo.IsMain = true;
            }

            user.Photos.Add(photo);

            if(await _repository.SaveAll())
            {
                //photo = await _repository.GetPhotoByPublicId(photo.PublicId);
                var photoToReturn = _mapper.Map<PhotoForReturnDto>(photo);
                return CreatedAtRoute("GetPhoto", new {id = photo.Id}, photoToReturn);
            }

            return BadRequest("Could not save photo");
        }

        [HttpPost("{id}/setMain")]
        public async Task<IActionResult> SetMainPhoto(int userId, int id)
        {
            if(userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            var photoFromRepo = await _repository.GetPhoto(id);
            if(photoFromRepo == null)
            {
                return NotFound();
            }

            if(photoFromRepo.IsMain)
            {
                return BadRequest("This is alredy the main photo");
            }

            var currentMainPhoto = await _repository.GetMainPhotoForUser(userId);
            if(currentMainPhoto != null)
            {
                currentMainPhoto.IsMain = false;
            }

            photoFromRepo.IsMain = true;

            if(await _repository.SaveAll())
            {
                return NoContent();
            }

            return BadRequest("Could not set photo to main");
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePhoto(int userId, int id)
        {
            if(userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
            {
                return Unauthorized();
            }

            var photoFromRepo = await _repository.GetPhoto(id);
            if(photoFromRepo == null)
            {
                return NotFound();
            }

            if(photoFromRepo.IsMain)
            {
                return BadRequest("You cannot delete the main photo");
            }

            if(photoFromRepo.PublicId != null)
            {
                var deleteParams = new DeletionParams(photoFromRepo.PublicId);
                var result = _cloudinary.Destroy(deleteParams);

                if(result.Result.Equals("ok"))
                {
                    _repository.Delete(photoFromRepo);
                }
            }
            else
            {
                _repository.Delete(photoFromRepo);
            }

            if(await _repository.SaveAll())
            {
                return Ok();
            }

            return BadRequest("Failed to delete the photo");
        }
    }
}